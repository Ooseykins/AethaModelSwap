using System;
using System.IO;
using Newtonsoft.Json;

namespace AethaModelSwapMod;

[Serializable]
public class ModelIKParameters
{
    [JsonIgnore] public string savePath = "";
    public int version = 1;
    public float verticalOffset = 0f;
    public float scale = 1f;

    public float stanceWidth = 1f;
    public float stanceHeight = 0.78f;
    public float footFrontBackOffset = 0f;
    public float strideLength = 1f;

    public float kneesOut = 1f;
    public float footAngle = 0f;
    public float armAngleOffset = 0f;
    public float handAngleOffset = 0f;
    public float spineAngleOffset = 0f;
    public float headAngleOffset = 0f;

    public float eyeMovement = 0f;

    public bool replaceStandardShader = false;

    public static ModelIKParameters LoadModelIKParameters(string path, bool setSavePath = false)
    {
        ModelIKParameters modelIKParameters = null;
        if (File.Exists(path))
        {
            modelIKParameters = JsonConvert.DeserializeObject<ModelIKParameters>(File.ReadAllText(path));
        }
        modelIKParameters ??= new ModelIKParameters();
        if (setSavePath)
        {
            modelIKParameters.savePath = path;
        }
        return modelIKParameters;
    }
    
    public static void SaveModelIKParameters(string path, ModelIKParameters modelIKParameters)
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(modelIKParameters));
    }
}
