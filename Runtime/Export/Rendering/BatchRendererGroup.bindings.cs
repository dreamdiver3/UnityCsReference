// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License


using System;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine.Scripting;
using UnityEngine.Bindings;

using Unity.Jobs;

namespace UnityEngine.Rendering
{
    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [NativeClass("BatchID")]
    [RequiredByNativeCode(Optional = true, GenerateProxy = true)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BatchID : IEquatable<BatchID>
    {
        public readonly static BatchID Null = new BatchID { value = 0 };

        public uint value;

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is BatchID)
            {
                return Equals((BatchID)obj);
            }

            return false;
        }

        public bool Equals(BatchID other)
        {
            return value == other.value;
        }

        public int CompareTo(BatchID other)
        {
            return value.CompareTo(other.value);
        }

        public static bool operator ==(BatchID a, BatchID b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BatchID a, BatchID b)
        {
            return !a.Equals(b);
        }
    }

    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [NativeClass("BatchMaterialID")]
    [RequiredByNativeCode(Optional = true, GenerateProxy = true)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BatchMaterialID : IEquatable<BatchMaterialID>
    {
        public readonly static BatchMaterialID Null = new BatchMaterialID { value = 0 };

        public uint value;

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is BatchMaterialID)
            {
                return Equals((BatchMaterialID)obj);
            }

            return false;
        }

        public bool Equals(BatchMaterialID other)
        {
            return value == other.value;
        }

        public int CompareTo(BatchMaterialID other)
        {
            return value.CompareTo(other.value);
        }

        public static bool operator ==(BatchMaterialID a, BatchMaterialID b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BatchMaterialID a, BatchMaterialID b)
        {
            return !a.Equals(b);
        }
    }

    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [NativeClass("BatchMeshID")]
    [RequiredByNativeCode(Optional = true, GenerateProxy = true)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BatchMeshID : IEquatable<BatchMeshID>
    {
        public readonly static BatchMeshID Null = new BatchMeshID { value = 0 };

        public uint value;

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is BatchMeshID)
            {
                return Equals((BatchMeshID)obj);
            }

            return false;
        }

        public bool Equals(BatchMeshID other)
        {
            return value == other.value;
        }

        public int CompareTo(BatchMeshID other)
        {
            return value.CompareTo(other.value);
        }

        public static bool operator ==(BatchMeshID a, BatchMeshID b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BatchMeshID a, BatchMeshID b)
        {
            return !a.Equals(b);
        }
    }

    // Match with BatchDrawCommandType in C++ side
    public enum BatchDrawCommandType : int
    {
        Direct = 0,
        Indirect = 1,
        Procedural = 2,
        ProceduralIndirect = 3,
    }

    // Match with BatchDrawCommandFlags in C++ side
    [Flags]
    public enum BatchDrawCommandFlags : int
    {
        None = 0,
        FlipWinding = 1 << 0, // Flip triangle winding when rendering, e.g. when the scale is negative
        HasMotion = 1 << 1, // Draw command contains at least one instance that requires per-object motion vectors
        IsLightMapped = 1 << 2, // Draw command contains lightmapped objects, which has implications for setting some lighting constants
        HasSortingPosition = 1 << 3, // Draw command instances have explicit world space float3 sorting positions to be used for depth sorting
        LODCrossFadeKeyword = 1 << 4, // Draw command instances have LOD_FADE_CROSSFADE keyword enabled
        LODCrossFadeValuePacked = 1 << 5, // Draw command instances have a 8-bit SNORM crossfade dither factor in the highest bits of their visible instance index
        LODCrossFade = LODCrossFadeKeyword | LODCrossFadeValuePacked,
        UseLegacyLightmapsKeyword = 1 << 6, // Draw command instances have USE_LEGACY_LIGHTMAPS keyword enabled
    }

    // Match with CullLightmappedShadowCasters in C++ side
    [Flags]
    public enum BatchCullingFlags : int
    {
        None = 0,
        CullLightmappedShadowCasters = 1 << 0,
    }

    // Match with BatchCullingViewType in C++ side
    public enum BatchCullingViewType : int
    {
        Unknown = 0,
        Camera = 1,
        Light = 2,
        Picking = 3,
        SelectionOutline = 4,
        Filtering = 5
    }

    // Match with BatchCullingProjectionType in C++ side
    public enum BatchCullingProjectionType : int
    {
        Unknown = 0,
        Perspective = 1,
        Orthographic = 2,
    }

    // Match with BatchBufferTarget in C++ side
    public enum BatchBufferTarget : int
    {
        Unknown = 0,
        UnsupportedByUnderlyingGraphicsApi = -1, // BRG not supported on this platform or graphics API
        RawBuffer = 1, // BRG supported using raw buffer instance data (SSBO)
        ConstantBuffer = 2, // BRG supported using constant buffer instance data (UBO)
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchPackedCullingViewID : IEquatable<BatchPackedCullingViewID>
    {
        internal ulong handle;

        public override int GetHashCode()
        {
            return handle.GetHashCode();
        }

        public bool Equals(BatchPackedCullingViewID other)
        {
            return handle == other.handle;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BatchPackedCullingViewID))
            {
                return false;
            }
            return this.Equals((BatchPackedCullingViewID)obj);
        }

        public static bool operator ==(BatchPackedCullingViewID lhs, BatchPackedCullingViewID rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(BatchPackedCullingViewID lhs, BatchPackedCullingViewID rhs)
        {
            return !lhs.Equals(rhs);
        }

        public BatchPackedCullingViewID(int instanceID, int sliceIndex)
        {
            handle = (uint) instanceID | ((ulong)sliceIndex << 32);
        }

        public int GetInstanceID()
        {
            return (int)(handle & 0xffffffff);
        }

        public int GetSliceIndex()
        {
            return (int)(handle >> 32);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchDrawCommand
    {
        public BatchDrawCommandFlags flags; // includes flipWinding and other dynamic flags
        public BatchID batchID;
        public BatchMaterialID materialID;
        public ushort splitVisibilityMask;
        public ushort lightmapIndex;
        public int sortingPosition; // If HasSortingPosition is set, this points to a float3 in instanceSortingPositions. If not, it will be directly casted into float and used as the distance.
        public uint visibleOffset;

        public uint visibleCount;
        public BatchMeshID meshID;
        public ushort submeshIndex;
        private ushort unusedPadding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchDrawCommandIndirect
    {
        public BatchDrawCommandFlags flags; // includes flipWinding and other dynamic flags
        public BatchID batchID;
        public BatchMaterialID materialID;
        public ushort splitVisibilityMask;
        public ushort lightmapIndex;
        public int sortingPosition; // If HasSortingPosition is set, this points to a float3 in instanceSortingPositions. If not, it will be directly casted into float and used as the distance.
        public uint visibleOffset;

        public BatchMeshID meshID;
        public MeshTopology topology;
        public GraphicsBufferHandle visibleInstancesBufferHandle;
        public uint visibleInstancesBufferWindowOffset;
        public uint visibleInstancesBufferWindowSizeBytes;
        public GraphicsBufferHandle indirectArgsBufferHandle;
        public uint indirectArgsBufferOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchDrawCommandProcedural
    {
        public BatchDrawCommandFlags flags; // includes flipWinding and other dynamic flags
        public BatchID batchID;
        public BatchMaterialID materialID;
        public ushort splitVisibilityMask;
        public ushort lightmapIndex;
        public int sortingPosition; // If HasSortingPosition is set, this points to a float3 in instanceSortingPositions. If not, it will be directly casted into float and used as the distance.
        public uint visibleOffset;

        public uint visibleCount;
        public MeshTopology topology;
        public GraphicsBufferHandle indexBufferHandle;
        public uint baseVertex;
        public uint indexOffsetBytes;
        public uint elementCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchDrawCommandProceduralIndirect
    {
        public BatchDrawCommandFlags flags; // includes flipWinding and other dynamic flags
        public BatchID batchID;
        public BatchMaterialID materialID;
        public ushort splitVisibilityMask;
        public ushort lightmapIndex;
        public int sortingPosition; // If HasSortingPosition is set, this points to a float3 in instanceSortingPositions. If not, it will be directly casted into float and used as the distance.
        public uint visibleOffset;

        public MeshTopology topology;
        public GraphicsBufferHandle indexBufferHandle;
        public GraphicsBufferHandle visibleInstancesBufferHandle;
        public uint visibleInstancesBufferWindowOffset;
        public uint visibleInstancesBufferWindowSizeBytes;
        public GraphicsBufferHandle indirectArgsBufferHandle;
        public uint indirectArgsBufferOffset;
    }

    // Match with BatchFilterSettings in C++ side
    [StructLayout(LayoutKind.Sequential)]
    public struct BatchFilterSettings
    {
        public uint renderingLayerMask;
        public int rendererPriority;
        private ulong m_sceneCullingMask;
        public byte layer;
        private byte m_batchLayer;
        private byte m_motionMode;
        private byte m_shadowMode;
        private byte m_receiveShadows;
        private byte m_staticShadowCaster;
        private byte m_allDepthSorted;
        private byte m_isSceneCullingMaskSet;

        public byte batchLayer
        {
            get => m_batchLayer;
            set => m_batchLayer = value;
        }

        public MotionVectorGenerationMode motionMode
        {
            get => (MotionVectorGenerationMode)m_motionMode;
            set => m_motionMode = (byte)value;
        }

        public ShadowCastingMode shadowCastingMode
        {
            get => (ShadowCastingMode)m_shadowMode;
            set => m_shadowMode = (byte)value;
        }

        public bool receiveShadows
        {
            get => m_receiveShadows != 0;
            set => m_receiveShadows = (byte)(value ? 1 : 0);
        }

        public bool staticShadowCaster
        {
            get => m_staticShadowCaster != 0;
            set => m_staticShadowCaster = (byte)(value ? 1 : 0);
        }

        public bool allDepthSorted
        {
            get => m_allDepthSorted != 0;
            set => m_allDepthSorted = (byte)(value ? 1 : 0);
        }

        [FreeFunction("BatchFilterSettings::DefaultCullingMask", IsThreadSafe = true)]
        private extern static ulong DefaultCullingMask();

        public ulong sceneCullingMask
        {
            get => (m_isSceneCullingMaskSet != 0) ? m_sceneCullingMask : DefaultCullingMask();
            set
            {
                m_isSceneCullingMaskSet = 1;
                m_sceneCullingMask = value;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchDrawRange
    {
        // Specifies which array of commands this range indexes into.
        public BatchDrawCommandType drawCommandsType;
        // The first BatchDrawCommand of this range is at this index in BatchCullingOutputDrawCommands.drawCommands
        public uint drawCommandsBegin;
        // How many BatchDrawCommand structs this range has. Can be 0 if there are no draws.
        public uint drawCommandsCount;
        // Filter settings for every draw in the range. If the filter settings don't match, the entire range can be skipped.
        public BatchFilterSettings filterSettings;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct BatchCullingOutputDrawCommands
    {
        // TempJob allocated by C#, released by C++
        public BatchDrawCommand* drawCommands;
        // TempJob allocated by C#, released by C++
        public BatchDrawCommandIndirect* indirectDrawCommands;
        // TempJob allocated by C#, released by C++
        public BatchDrawCommandProcedural* proceduralDrawCommands;
        // TempJob allocated by C#, released by C++
        public BatchDrawCommandProceduralIndirect* proceduralIndirectDrawCommands;
        // TempJob allocated by C#, released by C++
        public int* visibleInstances;
        // TempJob allocated by C#, released by C++
        public BatchDrawRange* drawRanges;
        // TempJob allocated by C#, released by C++
        public float* instanceSortingPositions;
        // TempJob allocated by C#, released by C++
        public int* drawCommandPickingInstanceIDs;
        public int drawCommandCount;
        public int indirectDrawCommandCount;
        public int proceduralDrawCommandCount;
        public int proceduralIndirectDrawCommandCount;
        public int visibleInstanceCount;
        public int drawRangeCount;
        public int instanceSortingPositionFloatCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MetadataValue
    {
        public int NameID;
        public uint Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [UsedByNativeCode]
    unsafe public struct CullingSplit
    {
        public Vector3 sphereCenter;
        public float sphereRadius;
        public int cullingPlaneOffset;
        public int cullingPlaneCount;
        public float cascadeBlendCullingFactor;
        public float nearPlane;
        public Matrix4x4 cullingMatrix;
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [UsedByNativeCode]
    unsafe public struct BatchCullingContext
    {
        internal BatchCullingContext(
            NativeArray<Plane> inCullingPlanes,
            NativeArray<CullingSplit> inCullingSplits,
            LODParameters inLodParameters,
            Matrix4x4 inLocalToWorldMatrix,
            BatchCullingViewType inViewType,
            BatchCullingProjectionType inProjectionType,
            BatchCullingFlags inBatchCullingFlags,
            ulong inViewID,
            uint inCullingLayerMask,
            ulong inSceneCullingMask,
            byte inExclusionSplitMask,
            int inReceiverPlaneOffset,
            int inReceiverPlaneCount,
            IntPtr inOcclusionBuffer)
        {
            cullingPlanes = inCullingPlanes;
            cullingSplits = inCullingSplits;
            lodParameters = inLodParameters;
            localToWorldMatrix = inLocalToWorldMatrix;
            viewType = inViewType;
            projectionType = inProjectionType;
            cullingFlags = inBatchCullingFlags;
            viewID = new BatchPackedCullingViewID { handle = inViewID };
            cullingLayerMask = inCullingLayerMask;
            sceneCullingMask = inSceneCullingMask;
            splitExclusionMask = inExclusionSplitMask;
            receiverPlaneOffset = inReceiverPlaneOffset;
            receiverPlaneCount = inReceiverPlaneCount;
#pragma warning disable CS0618 // Type or member is obsolete
            isOrthographic = 0;
#pragma warning restore CS0618 // Type or member is obsolete
            occlusionBuffer = inOcclusionBuffer;
        }

        readonly public NativeArray<Plane> cullingPlanes;
        readonly public NativeArray<CullingSplit> cullingSplits;
        readonly public LODParameters lodParameters;
        readonly public Matrix4x4 localToWorldMatrix;
        readonly public BatchCullingViewType viewType;
        readonly public BatchCullingProjectionType projectionType;
        readonly public BatchCullingFlags cullingFlags;
        readonly public BatchPackedCullingViewID viewID;
        readonly public uint cullingLayerMask;
        readonly public ulong sceneCullingMask;
        readonly public ushort splitExclusionMask;
        [System.Obsolete("BatchCullingContext.isOrthographic is deprecated. Use BatchCullingContext.projectionType instead.")]
        readonly public byte isOrthographic;
        readonly public int receiverPlaneOffset;
        readonly public int receiverPlaneCount;
        readonly internal IntPtr occlusionBuffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchCullingOutput
    {
        // One-element NativeArray to make it writable from C#
        public NativeArray<BatchCullingOutputDrawCommands> drawCommands;
        public NativeArray<IntPtr> customCullingResult;
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [UsedByNativeCode]
    unsafe struct BatchRendererCullingOutput
    {
        public JobHandle cullingJobsFence;
        public Matrix4x4 localToWorldMatrix;
        public Plane* cullingPlanes;
        public int cullingPlaneCount;
        public int receiverPlaneOffset;
        public int receiverPlaneCount;
        public CullingSplit* cullingSplits;
        public int cullingSplitCount;
        public BatchCullingViewType viewType;
        public BatchCullingProjectionType projectionType;
        public BatchCullingFlags cullingFlags;
        public ulong viewID;
        public uint  cullingLayerMask;
        public byte  splitExclusionMask;
        public ulong sceneCullingMask;
        public BatchCullingOutputDrawCommands* drawCommands;
        public uint brgId;
        public IntPtr occlusionBuffer;
        public IntPtr customCullingResult;
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    public struct ThreadedBatchContext
    {
        public IntPtr batchRendererGroup;

        [FreeFunction("BatchRendererGroup::AddDrawCommandBatch_Threaded", IsThreadSafe = true)]
        private extern static BatchID AddDrawCommandBatch(IntPtr brg, IntPtr values, int count, GraphicsBufferHandle buffer, uint bufferOffset, uint windowSize);

        [FreeFunction("BatchRendererGroup::SetDrawCommandBatchBuffer_Threaded", IsThreadSafe = true)]
        private extern static void SetDrawCommandBatchBuffer(IntPtr brg, BatchID batchID, GraphicsBufferHandle buffer);

        [FreeFunction("BatchRendererGroup::RemoveDrawCommandBatch_Threaded", IsThreadSafe = true)]
        private extern static void RemoveDrawCommandBatch(IntPtr brg, BatchID batchID);


        unsafe public BatchID AddBatch(NativeArray<MetadataValue> batchMetadata, GraphicsBufferHandle buffer)
        {
            return AddDrawCommandBatch(batchRendererGroup, (IntPtr)batchMetadata.GetUnsafeReadOnlyPtr(), batchMetadata.Length, buffer, 0, 0);
        }

        unsafe public BatchID AddBatch(NativeArray<MetadataValue> batchMetadata, GraphicsBufferHandle buffer, uint bufferOffset, uint windowSize)
        {
            return AddDrawCommandBatch(batchRendererGroup, (IntPtr)batchMetadata.GetUnsafeReadOnlyPtr(), batchMetadata.Length, buffer, bufferOffset, windowSize);
        }

        public void SetBatchBuffer(BatchID batchID, GraphicsBufferHandle buffer)
        {
            SetDrawCommandBatchBuffer(batchRendererGroup, batchID, buffer);
        }

        public void RemoveBatch(BatchID batchID)
        {
            RemoveDrawCommandBatch(batchRendererGroup, batchID);
        }
    }

    public struct BatchRendererGroupCreateInfo
    {
        public BatchRendererGroup.OnPerformCulling cullingCallback;
        public BatchRendererGroup.OnFinishedCulling finishedCullingCallback;
        public IntPtr userContext;
    };

    [StructLayout(LayoutKind.Sequential)]
    [NativeHeader("Runtime/Math/Matrix4x4.h")]
    [NativeHeader("Runtime/Camera/BatchRendererGroup.h")]
    [RequiredByNativeCode]
    public class BatchRendererGroup : IDisposable
    {
        IntPtr m_GroupHandle = IntPtr.Zero;
        OnPerformCulling m_PerformCulling;
        OnFinishedCulling m_FinishedCulling;

        unsafe public delegate JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext);
        unsafe public delegate void OnFinishedCulling(IntPtr customCullingResult);

        public unsafe BatchRendererGroup(OnPerformCulling cullingCallback, IntPtr userContext)
        {
            m_PerformCulling = cullingCallback;
            m_GroupHandle = Create(this, (void*)userContext);
        }

        public unsafe BatchRendererGroup(BatchRendererGroupCreateInfo info)
        {
            m_PerformCulling = info.cullingCallback;
            m_GroupHandle = Create(this, (void*)info.userContext);
            m_FinishedCulling = info.finishedCullingCallback;
        }

        public void Dispose()
        {
            Destroy(m_GroupHandle);
            m_GroupHandle = IntPtr.Zero;
        }

        public ThreadedBatchContext GetThreadedBatchContext()
        {
            return new ThreadedBatchContext { batchRendererGroup = m_GroupHandle };
        }

        private extern BatchID AddDrawCommandBatch(IntPtr values, int count, GraphicsBufferHandle buffer, uint bufferOffset, uint windowSize);
        unsafe public BatchID AddBatch(NativeArray<MetadataValue> batchMetadata, GraphicsBufferHandle buffer)
        {
            return AddDrawCommandBatch((IntPtr)batchMetadata.GetUnsafeReadOnlyPtr(), batchMetadata.Length, buffer, 0, 0);
        }
        unsafe public BatchID AddBatch(NativeArray<MetadataValue> batchMetadata, GraphicsBufferHandle buffer, uint bufferOffset, uint windowSize)
        {
            return AddDrawCommandBatch((IntPtr)batchMetadata.GetUnsafeReadOnlyPtr(), batchMetadata.Length, buffer, bufferOffset, windowSize);
        }

        private extern void RemoveDrawCommandBatch(BatchID batchID);
        public void RemoveBatch(BatchID batchID) { RemoveDrawCommandBatch(batchID); }

        private extern void SetDrawCommandBatchBuffer(BatchID batchID, GraphicsBufferHandle buffer);
        public void SetBatchBuffer(BatchID batchID, GraphicsBufferHandle buffer) { SetDrawCommandBatchBuffer(batchID, buffer); }

        public extern BatchMaterialID RegisterMaterial(Material material);
        internal extern void RegisterMaterials(ReadOnlySpan<int> materialID, Span<BatchMaterialID> batchMaterialID);

        public extern void UnregisterMaterial(BatchMaterialID material);
        public extern Material GetRegisteredMaterial(BatchMaterialID material);

        public extern BatchMeshID RegisterMesh(Mesh mesh);
        internal extern void RegisterMeshes(ReadOnlySpan<int> meshID, Span<BatchMeshID> batchMeshID);

        public extern void UnregisterMesh(BatchMeshID mesh);
        public extern Mesh GetRegisteredMesh(BatchMeshID mesh);

        public extern void SetGlobalBounds(Bounds bounds);

        public extern void SetPickingMaterial(Material material);
        public extern void SetErrorMaterial(Material material);
        public extern void SetLoadingMaterial(Material material);

        public extern void SetEnabledViewTypes(BatchCullingViewType[] viewTypes);

        private extern static BatchBufferTarget GetBufferTarget();
        public static BatchBufferTarget BufferTarget => GetBufferTarget();

        public extern static int GetConstantBufferMaxWindowSize();
        public extern static int GetConstantBufferOffsetAlignment();

        static extern unsafe IntPtr Create([Unmarshalled] BatchRendererGroup group, void* userContext);

        static extern void Destroy(IntPtr groupHandle);

        [RequiredByNativeCode]
        unsafe static void InvokeOnPerformCulling(BatchRendererGroup group, ref BatchRendererCullingOutput context, ref LODParameters lodParameters, IntPtr userContext)
        {
            NativeArray<Plane> cullingPlanes = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Plane>(context.cullingPlanes, context.cullingPlaneCount, Allocator.Invalid);
            NativeArray<CullingSplit> cullingSplits = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<CullingSplit>(context.cullingSplits, context.cullingSplitCount, Allocator.Invalid);
            NativeArray<BatchCullingOutputDrawCommands> drawCommands = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BatchCullingOutputDrawCommands>(
                context.drawCommands, 1, Allocator.Invalid);

            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref cullingPlanes, AtomicSafetyHandle.Create());
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref cullingSplits, AtomicSafetyHandle.Create());
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref drawCommands, AtomicSafetyHandle.Create());

            try
            {
                BatchCullingOutput cullingOutput = new BatchCullingOutput
                {
                    drawCommands = drawCommands,
                    customCullingResult = new NativeArray<IntPtr>(1, Allocator.Temp)
                };
                context.cullingJobsFence = group.m_PerformCulling(
                    group, new BatchCullingContext(
                        cullingPlanes,
                        cullingSplits,
                        lodParameters,
                        context.localToWorldMatrix,
                        context.viewType,
                        context.projectionType,
                        context.cullingFlags,
                        context.viewID,
                        context.cullingLayerMask,
                        context.sceneCullingMask,
                        context.splitExclusionMask,
                        context.receiverPlaneOffset,
                        context.receiverPlaneCount,
                        context.occlusionBuffer
                    ),
                    cullingOutput,
                    userContext
                );
                context.customCullingResult = cullingOutput.customCullingResult[0];
            }
            finally
            {
                JobHandle.ScheduleBatchedJobs();

                //@TODO: Check that the no jobs using the buffers have been scheduled that are not returned here...
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(cullingPlanes));
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(cullingSplits));
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(drawCommands));
            }
        }

        [RequiredByNativeCode]
        static void InvokeOnFinishedCulling(BatchRendererGroup group, IntPtr customCullingResult)
        {
            try
            {
                if(group.m_FinishedCulling != null) group.m_FinishedCulling(customCullingResult);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static class BindingsMarshaller
        {
            public static IntPtr ConvertToNative(BatchRendererGroup batchRendererGroup) => batchRendererGroup.m_GroupHandle;
        }

        [FreeFunction("BatchRendererGroup::OcclusionTestAABB", IsThreadSafe = true)]
        internal extern static bool OcclusionTestAABB(IntPtr occlusionBuffer, Bounds aabb);
    }
}
