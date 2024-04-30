// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements.Debugger;
using UnityEngine.UIElements.Layout;
using static UnityEngine.UIElements.Layout.LayoutNative;

namespace UnityEditor.UIElements.Experimental.UILayoutDebugger
{
    class LayoutPanelDebuggerImpl : PanelDebugger, StopRecordingInterface
    {
        const string k_DefaultStyleSheetPath = "UIPackageResources/StyleSheets/UILayoutDebugger/UILayoutDebugger.uss";

        private List<LayoutDebuggerItem> m_RecordLayout = null;

        int m_FrameIndex = 0;
        int m_PassIndex = 0;
        int m_LayoutLoop = 0;

        int m_MinFrameIndex = 0;
        int m_MaxFrameIndex = 0;

        Dictionary<int, int> m_MinMaxPassIndex = new Dictionary<int, int>();
        Dictionary<Tuple<int, int>, int> m_MinMaxLayoutLoop = new Dictionary<Tuple<int, int>, int>();

        int m_MinLayoutLoop = 0;
        int m_MaxLayoutLoop = 0;

        ToolbarToggle m_RecordLayoutToggle;
        ToolbarToggle m_StopRecordingOnError;
        Toggle m_FrameResetPassIndexLayoutLoop;
        Toggle m_PassResetLayoutLoop;
        Toggle m_LayoutLoopAllowFrameIndexPassIndexUpdate;
        VisualElement m_Interface;
        UILayoutDebuggerHistogram m_Histogram = null;
        MultiColumnListView m_Info = null;
        Label m_Label = null;
        UILayoutDebugger m_Display = null;
        SliderInt m_Slider = null;
        TextField m_SearchTextField;
        EnumFlagsField m_SearchModeEnumField;

        [Flags]
        enum SearchMode
        {
            ByName = 1,
            ByClass = 2
        };

        struct SearchInfo
        {
            public SearchMode m_SearchMode;
            public int m_FrameIndex;
            public int m_PassIndex;
            public int m_LayoutLoop;
            public bool m_FoundStartVE;
            public LayoutDebuggerVisualElement m_StartVE;
            public LayoutDebuggerVisualElement m_FoundVE;
        };

        SearchInfo m_SearchInfo;

        static UIRLayoutUpdater GetLayoutUpdater(IPanel panel)
        {
            return (panel as BaseVisualElementPanel)?.GetUpdater(VisualTreeUpdatePhase.Layout) as UIRLayoutUpdater;
        }

        internal void SetRecord(List<LayoutDebuggerItem> _recordLayout)
        {
            m_RecordLayout = _recordLayout;

            m_MinFrameIndex = Int32.MaxValue;
            m_MaxFrameIndex = -1;

            m_MinMaxLayoutLoop.Clear();
            m_MinMaxPassIndex.Clear();

            foreach (var record in m_RecordLayout)
            {
                m_MinFrameIndex = Math.Min(m_MinFrameIndex, record.m_FrameIndex);
                m_MaxFrameIndex = Math.Max(m_MaxFrameIndex, record.m_FrameIndex);

                if (m_MinMaxPassIndex.ContainsKey(record.m_FrameIndex))
                {
                    m_MinMaxPassIndex[record.m_FrameIndex] = Math.Max(m_MinMaxPassIndex[record.m_FrameIndex], record.m_PassIndex);
                }
                else
                {
                    m_MinMaxPassIndex.Add(record.m_FrameIndex, record.m_PassIndex);
                }

                Tuple<int, int> t = new Tuple<int, int>(record.m_FrameIndex, record.m_PassIndex);

                if (m_MinMaxLayoutLoop.ContainsKey(t))
                {
                    m_MinMaxLayoutLoop[t] = Math.Max(m_MinMaxLayoutLoop[t], record.m_LayoutLoop);
                }
                else
                {
                    m_MinMaxLayoutLoop.Add(t, 0);
                }
            }

            m_Display.SetRecord(_recordLayout);
            m_Histogram.m_Graph.SetRecord(_recordLayout);
        }
        public int UpdateSlider(int value, bool notify = true)
        {
            if (notify)
            {
                m_Slider.value = value;
            }
            else
            {
                m_Slider.SetValueWithoutNotify(value);
            }

            return (int)m_Slider.value;
        }

        public void UpdateLabel()
        {
            if (m_RecordLayout == null)
            {
                return;
            }

            int maxItem = 0;

            if (m_RecordLayout.Count == 0)
            {
                m_FrameIndex = 0;
                m_PassIndex = 0;
                m_MaxLayoutLoop = 0;
                return;
            }
            else
            {
                m_FrameIndex = Math.Clamp(m_FrameIndex, m_MinFrameIndex, m_MaxFrameIndex);
                m_PassIndex = Math.Clamp(m_PassIndex, 0, m_MinMaxPassIndex[m_FrameIndex]);
                m_MaxLayoutLoop = Math.Clamp(m_LayoutLoop, 0, m_MinMaxLayoutLoop[new Tuple<int, int>(m_FrameIndex, m_PassIndex)]);

                int maxZero = 0;
                int outOfRoot = 0;

                m_LayoutLoop = Math.Clamp(m_LayoutLoop, m_MinLayoutLoop, m_MaxLayoutLoop);

                foreach (var record in m_RecordLayout)
                {
                    if (record.m_FrameIndex == m_FrameIndex)
                    {
                        if (record.m_PassIndex == m_PassIndex)
                        {
                            if (record.m_LayoutLoop == m_LayoutLoop)
                            {
                                Rect rect = new Rect();
                                rect.x = record.m_VE.layout.x;
                                rect.y = record.m_VE.layout.y;
                                rect.width = record.m_VE.layout.width;
                                rect.height = record.m_VE.layout.height;

                                UILayoutDebugger.CountLayoutItem(rect, record.m_VE, ref maxItem, ref maxZero, ref outOfRoot);

                                break;
                            }
                        }
                    }
                }
            }

            m_Label.text = "Informations:" +
                " FrameIndex(" + m_MinFrameIndex + ", " + m_MaxFrameIndex + "):" + m_FrameIndex +
                " PassIndex(" + 0 + ", " + m_MinMaxPassIndex[m_FrameIndex] + "):" + m_PassIndex +
                " LayoutLoop(" + 0 + ", " + m_MinMaxLayoutLoop[new Tuple<int, int>(m_FrameIndex, m_PassIndex)] + "):" + m_LayoutLoop;

            m_Slider.lowValue = 0;
            m_Slider.highValue = maxItem-1;
            m_Slider.value = maxItem-1;
            m_Slider.MarkDirtyRepaint();
        }

        private static bool IsEqual(LayoutDebuggerVisualElement a, LayoutDebuggerVisualElement b)
        {
            if (a.name != b.name)
            {
                return false;
            }

            if (a.layout != b.layout)
            {
                return false;
            }

            if (a.visible != b.visible)
            {
                return false;
            }

            if (a.enabledInHierarchy != b.enabledInHierarchy)
            {
                return false;
            }

            if ((a.m_Children == null) != (b.m_Children == null))
            {
                return false;
            }

            if ((a.m_Children != null) && (b.m_Children != null))
            {
                if (a.m_Children.Count != b.m_Children.Count)
                {
                    return false;
                }

                for (int i = 0; i < a.m_Children.Count; i++)
                {
                    if (!IsEqual(a.m_Children[i], b.m_Children[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void FindNextComplexUpdateLoop()
        {
            int nextFrameIndex = m_FrameIndex + 1;

            for (int i = 0; i < m_RecordLayout.Count; i++)
            {
                if (m_RecordLayout[i].m_FrameIndex == nextFrameIndex)
                {
                    for (; i < m_RecordLayout.Count; i++)
                    {
                        int j = i + 1;

                        int bestFrameIndex = -1;

                        for (; j < m_RecordLayout.Count; j++)
                        {
                            if (m_RecordLayout[i].m_FrameIndex == m_RecordLayout[j].m_FrameIndex)
                            {
                                if (m_RecordLayout[i].m_PassIndex == m_RecordLayout[j].m_PassIndex)
                                {
                                    if (m_RecordLayout[i].m_LayoutLoop == 0 && m_RecordLayout[j].m_LayoutLoop > 1)
                                    {
                                        bestFrameIndex = m_RecordLayout[i].m_FrameIndex;
                                    }
                                }
                            }
                        }

                        if (bestFrameIndex != -1)
                        {
                            m_FrameIndex = bestFrameIndex;
                            return;
                        }
                    }

                    break;
                }
            }
        }

        public void StopRecording()
        {
            m_RecordLayoutToggle.value = false;
        }

        protected override void SelectPanelToDebug(IPanelChoice pc)
        {
            if (pc != selectedPanel)
            {
                StopRecording();
                LayoutTraceEnable = false;
            }

            base.SelectPanelToDebug(pc);

            TraceToggle.SetValueWithoutNotify(LayoutTraceEnable);
        }

        // Change this to print the trace to the console as soon as it is received
        // It could be usefull in case of crash, but there will be a stacktrace for every logs
        private const bool bundleTracesForLogging = true;
        private readonly List<LayoutNative.LayoutLogData> traces = new();

        private ToolbarToggle TraceToggle;

        bool LayoutTraceEnable
        {
            get { return (panel as BaseVisualElementPanel)?.layoutConfig.ShouldLog ?? false;  }
            set
            {
                if (panel is BaseVisualElementPanel basePanel)
                {
                    if(basePanel.layoutConfig.ShouldLog == value)
                        return;

                    FlushTraces();// flush when both turning on and off, in case ther is something pending in the buffer
                    Debug.Log( $"Setting layout to be logged to {value} on {selectedPanel}");
                    basePanel.layoutConfig.ShouldLog = value;

                    if (value)
                        LayoutNative.onLayoutLog += onLog;
                    else
                        LayoutNative.onLayoutLog -= onLog;

                }
            }
        }
        private void FlushTraces()
        {
            if(traces.Count ==0)
                return;

            var sb = StringBuilderPool.Get();
            foreach(var trace in traces)
            {
                sb.AppendLine(PrettyTrace(trace));
            }

            //Log without stacktrace
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, sb.ToString());

            traces.Clear();
            StringBuilderPool.Release(sb);
        }

        private string PrettyName(LayoutNode node)
        {
             //Quick path is only if the node has measure for now.
            var owner = node.GetOwner() ?? FindHandleInDescendents(selectedPanel.panel.GetRootVisualElement(), node);
            string name = owner?.name;
            string type = owner?.typeName ?? string.Empty;
            var deptj = 0;
            var current = node;
            while (!current.Parent.IsUndefined)
            {
                deptj++;
                current = current.Parent;
            }
            return $"{"".PadLeft(deptj*2)}[{node.Handle.Index,4},{type},{name,8}]";
        }

        private string PrettyTrace(LayoutNative.LayoutLogData data)
        {
            return $"{PrettyName(data.node)} : {data.eventType} : {data.message}";
        }

        private VisualElement FindHandleInDescendents(VisualElement ve, LayoutNode node)
        {
            if (ve.layoutNode.Handle.Index == node.Handle.Index)
                return ve;

            foreach (var child in ve.hierarchy.Children())
            {
                var found = FindHandleInDescendents(child, node);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void onLog(LayoutNative.LayoutLogData data)
        {
            if (bundleTracesForLogging)
            {
                traces.Add(data);
                if (data.eventType == LayoutLogEventType.EndLayout)
                    FlushTraces();
            }
            else
            {
                // this is a compile time debug option.
#pragma warning disable CS0162 // Unreachable code detected
                //Log without stacktrace
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, PrettyTrace(data));
#pragma warning restore CS0162 // Unreachable code detected
            }
        }

        private bool AreTracesAvailable()
        {
            bool hasTraces = false;
            Action<LayoutLogData> callback = (_) => { hasTraces = true; };

            LayoutNative.onLayoutLog += callback;
            LayoutProcessor.CalculateLayout(LayoutNode.Undefined, 0, 0, 0);
            LayoutNative.onLayoutLog -= callback;

            return hasTraces;
        }

        public void Initialize(EditorWindow debuggerWindow, VisualElement root)
        {
            base.Initialize(debuggerWindow);
            var sheet = EditorGUIUtility.Load(k_DefaultStyleSheetPath) as StyleSheet;
            root.styleSheets.Add(sheet);
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 0;
            root.style.flexShrink = 0;

            m_PanelSelect.style.flexShrink = 0;

            m_RecordLayoutToggle = new ToolbarToggle() { name = "recordLayoutToggle" };
            m_RecordLayoutToggle.text = "Record Layout Updates";
            m_RecordLayoutToggle.RegisterValueChangedCallback((e) =>
            {
                if (selectedPanel != null)
                {
                    var layoutUpdater = GetLayoutUpdater(selectedPanel.panel);
                    layoutUpdater.recordLayout = e.newValue;

                    if (e.newValue == false)
                    {
                        UnityEditor.EditorApplication.update -= UIRLayoutUpdater.IncrementMainLoopCount;

                        m_RecordLayoutToggle.text = "Record Layout Updates (" + layoutUpdater.recordLayoutCount + ")";
                        m_Interface.SetEnabled(true);
                        SetRecord(layoutUpdater.GetListOfRecord());
                        UpdateLabelsAndSetupIndices();
                        m_Display.SetMaxItem((int)m_Slider.value);
                    }
                    else
                    {
                        UnityEditor.EditorApplication.update += UIRLayoutUpdater.IncrementMainLoopCount;
                    }
                }
            });
            m_Toolbar.Add(m_RecordLayoutToggle);

            m_StopRecordingOnError = new ToolbarToggle();
            m_StopRecordingOnError.text = "Stop recording on 'Layout update is struggling' error";
            m_StopRecordingOnError.RegisterValueChangedCallback((e) =>
            {
                if (e.newValue)
                {
                    UIRLayoutUpdater.s_StopRecording = this;
                }
                else
                {
                    UIRLayoutUpdater.s_StopRecording = null;
                }
            });
            m_Toolbar.Add(m_StopRecordingOnError);


            TraceToggle = new ToolbarToggle() { name = "Trace Layout" };
            TraceToggle.text = "Trace Layout";
            if (AreTracesAvailable())
            {
                TraceToggle.value = LayoutTraceEnable;
                TraceToggle.RegisterValueChangedCallback((e) =>
                {
                    LayoutTraceEnable = e.newValue;
                });
            }
            else
            {
                TraceToggle.SetEnabled(false);
                TraceToggle.tooltip = "Tracing is not available in this build.";
            }
            m_Toolbar.Add(TraceToggle);

            root.Add(m_Toolbar);


            VisualElement histogramCol = createNewColumn();

            VisualElement row = createNewRow();

            m_Label = new UnityEngine.UIElements.Label();
            row.Add(m_Label);
            histogramCol.Add(row);

            m_Display = new UILayoutDebugger();
            m_Display.m_ParentWindow = this;
            m_Display.style.flexDirection = FlexDirection.Column;
            m_Display.style.flexShrink = 0;

            row = createNewRow();

            Button firstFrameIndex = new Button();
            firstFrameIndex.text = "Goto first Frame Index";
            firstFrameIndex.clicked += () =>
            {
                m_FrameIndex = 0;
                m_PassIndex = 0;
                m_LayoutLoop = 0;
                UpdateLabelsAndSetupIndices();
            };
            row.Add(firstFrameIndex);

            Button gotoNextComplexUpdateLoop = new Button();
            gotoNextComplexUpdateLoop.text = "Goto next complex update";
            gotoNextComplexUpdateLoop.clicked += () =>
            {
                FindNextComplexUpdateLoop();
                UpdateLabelsAndSetupIndices();
            };
            row.Add(gotoNextComplexUpdateLoop);
            histogramCol.Add(row);

            row = createNewRow();

            // Use minimum height to simulate grid layout.

            const float minHeight = 20;
            VisualElement column = createNewColumn();

            {
                Button frameIndexDown = new Button();
                frameIndexDown.style.minHeight = minHeight;
                frameIndexDown.text = "Frame Index--";
                frameIndexDown.clicked += () =>
                {
                    m_FrameIndex--;
                    UpdateLabel();
                    m_LayoutLoop = m_MaxLayoutLoop;
                    SetupIndices();
                };
                column.Add(frameIndexDown);

                Button passIndexLoopDown = new Button();
                passIndexLoopDown.style.minHeight = minHeight;
                passIndexLoopDown.text = "Pass Index--";
                passIndexLoopDown.clicked += () =>
                {
                    m_PassIndex--;
                    UpdateLabelsAndSetupIndices();
                };
                column.Add(passIndexLoopDown);

                Button layoutLoopDown = new Button();
                layoutLoopDown.style.minHeight = minHeight;
                layoutLoopDown.text = "Layout Loop--";
                layoutLoopDown.clicked += () =>
                {
                    m_LayoutLoop--;

                    if (m_LayoutLoopAllowFrameIndexPassIndexUpdate.value)
                    {
                        if (m_LayoutLoop < 0)
                        {
                            if (m_PassIndex == 0)
                            {
                                if (m_FrameIndex > m_MinFrameIndex)
                                {
                                    m_FrameIndex--;
                                    m_FrameIndex = Math.Clamp(m_FrameIndex, m_MinFrameIndex, m_MaxFrameIndex);
                                    m_PassIndex = m_MinMaxPassIndex[m_FrameIndex];
                                    m_LayoutLoop = m_MinMaxLayoutLoop[new Tuple<int, int>(m_FrameIndex, m_PassIndex)];
                                }
                                else
                                {
                                    m_PassIndex = 0;
                                    m_LayoutLoop = 0;
                                }
                            }
                            else
                            {
                                m_PassIndex--;
                                m_LayoutLoop = m_MinMaxLayoutLoop[new Tuple<int, int>(m_FrameIndex, m_PassIndex)];
                            }
                        }
                    }

                    UpdateLabelsAndSetupIndices();

                };
                column.Add(layoutLoopDown);
            }
            row.Add(column);

            column = createNewColumn();
            {
                Button frameIndexUp = new Button();
                frameIndexUp.style.minHeight = minHeight;
                frameIndexUp.text = "Frame Index++";
                frameIndexUp.clicked += () =>
                {
                    m_FrameIndex++;
                    UpdateLabel();
                    m_LayoutLoop = m_MaxLayoutLoop;
                    SetupIndices();

                };
                column.Add(frameIndexUp);

                Button passIndexUp = new Button();
                passIndexUp.style.minHeight = minHeight;
                passIndexUp.text = "Pass Index++";
                passIndexUp.clicked += () =>
                {
                    m_PassIndex++;
                    UpdateLabelsAndSetupIndices();
                };
                column.Add(passIndexUp);

                Button layoutLoopUp = new Button();
                layoutLoopUp.style.minHeight = minHeight;
                layoutLoopUp.text = "Layout Loop++";
                layoutLoopUp.clicked += () =>
                {
                    m_LayoutLoop++;
                    if (m_LayoutLoopAllowFrameIndexPassIndexUpdate.value)
                    {
                        if (m_LayoutLoop > m_MinMaxLayoutLoop[new Tuple<int, int>(m_FrameIndex, m_PassIndex)])
                        {
                            if (m_PassIndex < m_MinMaxPassIndex[m_FrameIndex])
                            {
                                m_PassIndex++;
                                m_LayoutLoop = 0;
                            }
                            else
                            {
                                if (m_FrameIndex < m_MaxFrameIndex)
                                {
                                    m_FrameIndex ++;
                                    m_PassIndex = 0;
                                    m_LayoutLoop = 0;
                                }
                            }
                        }
                    }

                    UpdateLabelsAndSetupIndices();
                };
                column.Add(layoutLoopUp);
            }
            row.Add(column);

            column = createNewColumn();
            {
                m_FrameResetPassIndexLayoutLoop = new Toggle();
                m_FrameResetPassIndexLayoutLoop.style.minHeight = minHeight;
                m_FrameResetPassIndexLayoutLoop.text = "Reset Pass and Layout on change";
                m_FrameResetPassIndexLayoutLoop.SetEnabled(false);
                column.Add(m_FrameResetPassIndexLayoutLoop);

                m_PassResetLayoutLoop = new Toggle();
                m_PassResetLayoutLoop.style.minHeight = minHeight;
                m_PassResetLayoutLoop.text = "Reset Layout on change";
                m_PassResetLayoutLoop.SetEnabled(false);
                column.Add(m_PassResetLayoutLoop);

                m_LayoutLoopAllowFrameIndexPassIndexUpdate = new Toggle();
                m_LayoutLoopAllowFrameIndexPassIndexUpdate.style.minHeight = minHeight;
                m_LayoutLoopAllowFrameIndexPassIndexUpdate.text = "Update FrameIndex and PassIndex on underflow/overflow";
                column.Add(m_LayoutLoopAllowFrameIndexPassIndexUpdate);

            }

            row.Add(column);
            histogramCol.Add(row);

            row = createNewRow();

            m_SearchTextField = new TextField();
            m_SearchTextField.style.flexGrow = 1.0f;
            m_SearchTextField.style.flexShrink = 0.0f;
            m_SearchTextField.style.minWidth = 50.0f;
            row.Add(m_SearchTextField);

            m_SearchModeEnumField = new EnumFlagsField("Search mode", SearchMode.ByClass);
            m_SearchModeEnumField.labelElement.style.minWidth = 50.0f;

            row.Add(m_SearchModeEnumField);

            Button button = new Button();
            button.text = "Search";
            button.clicked += () =>
            {
                SearchVE();
            };

            row.Add(button);

            button = new Button();
            button.text = "Search Next";
            row.Add(button);
            button.clicked += () =>
            {
                SearchNextVE();
            };

            histogramCol.Add(row);

            row = createNewRow();

            m_Info = new MultiColumnListView();

            m_Info.style.marginRight = 2;
            m_Info.style.marginLeft = 2;
            m_Info.style.marginBottom = 2;
            m_Info.style.marginTop = 2;

            m_Info.style.flexGrow = 1;
            m_Info.style.flexShrink = 1;

            Column header = new Column() { name = "Pos" };
            header.title = "Pos";
            header.width = 50;
            header.makeCell = () => new Label();
            m_Info.columns.Add(header);

            header = new Column() { name = "FrameIndex" };
            header.title = "FrameIndex";
            header.width = 100;
            header.makeCell = () => new Label();
            m_Info.columns.Add(header);

            header = new Column() { name = "PassIndex" };
            header.title = "PassIndex";
            header.width = 100;
            header.makeCell = () => new Label();
            m_Info.columns.Add(header);

            header = new Column() { name = "LayoutLoop" };
            header.title = "LayoutLoop";
            header.width = 100;
            header.makeCell = () => new Label();
            m_Info.columns.Add(header);

            header = new Column() { name = "VisualElement" };
            header.title = "VisualElement";
            header.width = 800;
            header.stretchable = true;

            header.makeCell = () => new Label();
            m_Info.columns.Add(header);
            m_Info.columns.stretchMode = Columns.StretchMode.GrowAndFill;

            m_Info.selectionChanged += OnSelectionChanged;

            histogramCol.Add(row);

            VisualElement histogramRow = createNewRow();
            histogramRow.Add(histogramCol);
            m_Histogram = new UILayoutDebuggerHistogram();
            histogramRow.Add(m_Histogram);

            m_Histogram.m_Graph.RegisterCallback<MouseMoveEvent>(HistogramOnMouseMove);
            m_Histogram.m_Graph.RegisterCallback<MouseDownEvent>(HistogramOnMouseDown);
            m_Histogram.m_Graph.RegisterCallback<MouseLeaveEvent>(HistogramOnMouseLeave);

            root.Add(histogramRow);

            row = createNewRow();

            m_Slider = new SliderInt("Last VE");
            m_Slider.lowValue = 0;
            m_Slider.highValue = 0;
            m_Slider.showInputField = true;
            m_Slider.pageSize = 1;
            m_Slider.style.flexGrow = 1;
            m_Slider.RegisterValueChangedCallback(v =>
            {
                m_Display.SetMaxItem((int)v.newValue);
                m_Display.FillUpdateInfoOfSelectedElement(m_Info);
            });

            m_Interface = createNewColumn();
            m_Interface.Add(row);

            row = createNewRow();

            row.Add(m_Slider);

            UpdateLabelsAndSetupIndices();
            m_Display.SetMaxItem((int)m_Slider.value);

            m_Interface.Add(row);

            row = createNewRow();

            Toggle toggle = new Toggle();
            toggle.text = "Only show IsDirty=true";
            toggle.value = false;
            toggle.RegisterValueChangedCallback((e) =>
            {
                m_Display.SetShowDirty(e.newValue);
                m_Display.FillUpdateInfoOfSelectedElement(m_Info);
            });
            row.Add(toggle);

            toggle = new Toggle() { name = "lockVisualElementToggle" };
            toggle.text = "Lock the currently selected VisualElement";
            toggle.value = false;
            toggle.RegisterValueChangedCallback((e) =>
            {
                m_Display.LockSelectedElement(e.newValue);
            });

            row.Add(toggle);
            m_Interface.Add(row);

            m_Interface.SetEnabled(false);

            root.Add(m_Interface);

            row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow = 1;
            row.Add(m_Display);
            row.Add(m_Info);

            root.Add(row);
        }

        private void SetupIndices()
        {
            m_Display.SetIndices(m_FrameIndex, m_PassIndex, m_LayoutLoop);
            m_Histogram.m_Graph.SelectItemFromIndices(m_FrameIndex, m_PassIndex, m_LayoutLoop);
        }

        void OnSelectionChanged(IEnumerable<object> items)
        {
            foreach(object item in items)
            {
                m_Display.lastDrawElement = ((UILayoutDebugger.InfoData)item).m_VE;
                m_Display.MarkDirtyRepaint();
                break;
            }
        }

        private void UpdateLabelsAndSetupIndices()
        {
            UpdateLabel();
            SetupIndices();
        }

        private void SearchVE()
        {
            m_SearchInfo.m_SearchMode = (SearchMode)m_SearchModeEnumField.value;
            m_SearchInfo.m_StartVE = null;
            Search();
        }

        private void SearchNextVE()
        {
            m_SearchInfo.m_StartVE = m_Display.lastDrawElement;
            Search();
        }

        private void Search()
        {
            if (m_RecordLayout == null)
            {
                Debug.LogWarning("No recorded data to search.");
                return;
            }

            if (string.IsNullOrWhiteSpace(m_SearchTextField.text))
            {
                Debug.LogWarning("Search field empty.");
                return;
            }

            m_SearchInfo.m_FoundStartVE = false;
            m_SearchInfo.m_FoundVE = null;

            bool startSearch = m_SearchInfo.m_StartVE == null;

            for (int i = 0; i < m_RecordLayout.Count; i++)
            {
                if (!startSearch)
                {
                    if ((m_RecordLayout[i].m_FrameIndex == m_SearchInfo.m_FrameIndex) &&
                         (m_RecordLayout[i].m_PassIndex == m_SearchInfo.m_PassIndex) &&
                         (m_RecordLayout[i].m_LayoutLoop == m_SearchInfo.m_LayoutLoop))
                    {
                        startSearch = true;
                    }
                }

                if (startSearch)
                {
                    SearchElement(m_RecordLayout[i].m_VE);
                    if (m_SearchInfo.m_FoundVE != null)
                    {
                        m_FrameIndex = m_RecordLayout[i].m_FrameIndex;
                        m_PassIndex = m_RecordLayout[i].m_PassIndex;
                        m_LayoutLoop = m_RecordLayout[i].m_LayoutLoop;

                        m_SearchInfo.m_FrameIndex = m_FrameIndex;
                        m_SearchInfo.m_PassIndex = m_PassIndex;
                        m_SearchInfo.m_LayoutLoop = m_LayoutLoop;

                        UpdateLabelsAndSetupIndices();
                        m_Display.lastDrawElement = m_SearchInfo.m_FoundVE;
                        break;
                    }
                }
            }

            if (m_SearchInfo.m_FoundVE == null)
            {
                Debug.LogWarning("Last item reached.");
            }

        }

        private void SearchElement(LayoutDebuggerVisualElement ve)
        {
            if (m_SearchInfo.m_FoundVE != null)
            {
                return;
            }

            if (!ve.IsVisualElementVisible())
            {
                return;
            }

            if (m_SearchInfo.m_StartVE != null && m_SearchInfo.m_FoundStartVE == false)
            {
                if (ve == m_SearchInfo.m_StartVE)
                {
                    m_SearchInfo.m_FoundStartVE = true;
                }
            }
            else
            {
                var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                var options = CompareOptions.IgnoreCase;

                if ((ve.m_OriginalVisualElement != null) && (ve.m_OriginalVisualElement.name != null) &&
                    (((m_SearchInfo.m_SearchMode & SearchMode.ByName) != 0) && ((compareInfo.IndexOf(ve.m_OriginalVisualElement.name, m_SearchTextField.text, options) != -1))) ||
                    (((m_SearchInfo.m_SearchMode & SearchMode.ByClass) != 0) && ((compareInfo.IndexOf(ve.m_OriginalVisualElement.GetType().ToString(), m_SearchTextField.text, options) != -1))))
                {
                    m_SearchInfo.m_FoundVE = ve;
                    return;
                }
            }

            for (int i = 0; i < ve.m_Children.Count; ++i)
            {
                var child = ve.m_Children[i];
                SearchElement(child);
            }
        }

        private void HistogramOnMouseMove(MouseMoveEvent evt)
        {
            m_Histogram.m_Graph.OnMouseMove(evt);
        }

        private void HistogramOnMouseDown(MouseDownEvent evt)
        {
            m_Histogram.m_Graph.SelectItem();
            int selectedItem = m_Histogram.m_Graph.GetSelectedLayoutDebuggerItem();

            if (selectedItem >= 0)
            {
                m_Display.SetItemIndex(selectedItem);
                m_FrameIndex = m_RecordLayout[selectedItem].m_FrameIndex;
                m_PassIndex = m_RecordLayout[selectedItem].m_PassIndex;
                m_LayoutLoop = m_RecordLayout[selectedItem].m_LayoutLoop;
                UpdateLabel();
                m_Display.FillUpdateInfoOfSelectedElement(m_Info);
            }
        }

        private void HistogramOnMouseLeave(MouseLeaveEvent evt)
        {
            m_Histogram.m_Graph.DisableHover();
        }

        public void UpdateInfo()
        {
            m_Display.FillUpdateInfoOfSelectedElement(m_Info);
        }

        private VisualElement createNewRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow = 0;
            row.style.flexShrink = 0;
            return row;
        }

        private VisualElement createNewColumn()
        {
            VisualElement column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 0;
            column.style.flexShrink = 0;
            return column;
        }

    }
}
