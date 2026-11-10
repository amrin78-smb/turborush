using UnityEditor;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Import settings for the Blender car FBX files in <see cref="CarBuilder.SourceFolder"/> (copied there from Art/Cars by
    /// Tools/setup.sh). Metres, axis conversion baked so nothing is rotated, hierarchy kept so parts are found by name,
    /// material names kept (they carry the colour slots), no animation, cameras or lights. The FBX meshes are only read
    /// by CarBuilder at setup time; the game uses the converted meshes.
    /// </summary>
    public sealed class CarModelImport : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CarBuilder.SourceFolder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.preserveHierarchy = true;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = true;
            mi.weldVertices = false;
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = 0f;
            mi.addCollider = false;
        }
    }
}
