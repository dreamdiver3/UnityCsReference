// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEditor.Build.Profile.Handlers;
using UnityEngine.UIElements;

namespace UnityEditor.Build.Profile.Elements
{
    /// <summary>
    /// List item showing a build profile name and icon in the <see cref="BuildProfileWindow"/>
    /// classic platform or build profile columns. It's possible to edit this label.
    /// </summary>
    internal class BuildProfileListEditableLabel : BuildProfileListLabel
    {
        protected override string k_Uxml => "BuildProfile/UXML/BuildProfileEditableLabelElement.uxml";

        bool m_IsIndicatorActiveOnEdit;
        TextField m_TextField;
        Func<object, string, bool> m_OnNameChanged;
        BuildProfileRenameOverlay m_RenameOverlay;

        internal BuildProfileListEditableLabel(Func<object, string, bool> onNameChanged)
        {
            m_OnNameChanged = onNameChanged;
            m_TextField = this.Q<TextField>("profile-list-text-field");
            m_TextField.RegisterCallback<FocusOutEvent>(OnEditTextFinished);
            m_TextField.RegisterValueChangedCallback(OnTextFieldValueChange);
            m_TextField.Hide();

            m_RenameOverlay = new BuildProfileRenameOverlay(m_TextField);
            this.AddToClassList("pl-large");
        }

        internal BuildProfileListEditableLabel(Func<object, string, bool> onNameChanged, IManipulator manipulator)
            : this(onNameChanged)
        {
            this.AddManipulator(manipulator);
        }

        internal void UnbindItem()
        {
            m_TextField.UnregisterCallback<FocusOutEvent>(OnEditTextFinished);
            m_TextField.UnregisterValueChangedCallback(OnTextFieldValueChange);
            m_RenameOverlay.OnRenameEnd();
        }

        internal void EditName()
        {
            m_TextField.value = m_Text.text;

            m_Text.Hide();
            m_TextField.Show();
            m_TextField.Focus();

            m_IsIndicatorActiveOnEdit = m_ActiveIndicator.style.display != DisplayStyle.None;
            if (m_IsIndicatorActiveOnEdit)
            {
                SetActiveIndicator(false);
            }
        }

        void OnEditTextFinished(FocusOutEvent evt)
        {
            m_TextField.Hide();

            if (m_OnNameChanged.Invoke(dataSource, m_TextField.value))
                m_Text.text = m_TextField.value;

            m_Text.Show();

            if (m_IsIndicatorActiveOnEdit)
                SetActiveIndicator(true);

            m_RenameOverlay.OnRenameEnd();
        }

        void OnTextFieldValueChange(ChangeEvent<string> evt)
        {
            if (string.IsNullOrEmpty(evt.newValue))
                return;

            m_RenameOverlay.OnNameChanged(evt.previousValue, evt.newValue);
        }
    }
}
