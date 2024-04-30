// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Linq;

namespace UnityEditor.PackageManager.UI.Internal
{
    internal class RemoveImportedFoldoutGroup : MultiSelectFoldoutGroup
    {
        public RemoveImportedFoldoutGroup(IApplicationProxy applicationProxy, IPackageOperationDispatcher operationDispatcher)
            : base(new RemoveImportedAction(operationDispatcher, applicationProxy))
        {
        }

        public override void Refresh()
        {
            mainFoldout.headerTextTemplate = L10n.Tr("Remove imported assets from {0}");
            inProgressFoldout.headerTextTemplate = L10n.Tr("Removing imported assets from {0}");
            base.Refresh();
        }

        public override bool AddPackage(IPackage package)
        {
            return package.versions.primary.importedAssets?.Any() == true && base.AddPackage(package);
        }
    }
}
