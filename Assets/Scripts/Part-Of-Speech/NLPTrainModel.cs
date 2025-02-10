using UnityEngine;
using OpenNLP.Tools.PosTagger;
using SharpEntropy.IO;
using Voxell.Inspector;
using SharpEntropy;
using Voxell;

public class NLPTrainModel : MonoBehaviour
{
    /// <summary>
    /// The file with the training samples; works also with an array of files
    /// </summary>
    [StreamingAssetFilePath] public string trainingFile;
    /// <summary>
    /// The number of iterations; no general rule for finding the best value, just try several!
    /// </summary>
    public int iterations = 5;
    /// <summary>
    /// The cut; no general rule for finding the best value, just try several!
    /// </summary>
    public int cut = 2;

    /// <summary>
    /// Persist the model to use it later
    /// </summary>
    [StreamingAssetFilePath] public string outputFilePath;

    private GisModel bestModel;

    [NaughtyAttributes.Button]
    public void TrainPOSModel()
    {
        GisModel model = MaximumEntropyPosTagger.TrainModel(FileUtilx.GetStreamingAssetFilePath(trainingFile), iterations, cut);
        new BinaryGisModelWriter().Persist(model, FileUtilx.GetStreamingAssetFilePath(outputFilePath));
    }
}