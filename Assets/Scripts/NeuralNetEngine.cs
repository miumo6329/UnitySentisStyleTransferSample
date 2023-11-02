using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Sentis;
using System.Linq;

/*
 *  Neural net engine and handles the inference.
 */

public class NeuralNetEngine : MonoBehaviour
{
    public ModelAsset onnxModel;

    // engine type
    IWorker engine;

    // This small model works just as fast on the CPU as well as the GPU:
    static Unity.Sentis.BackendType backendType = Unity.Sentis.BackendType.GPUCompute;

    // width and height of the image:
    const int imageWidth = 224;

    // input tensor
    TensorFloat inputTensor = null;

    // op to manipulate Tensors 
    Ops ops;

    public Camera lookCamera;

    public Material display;

    int i = 0;



    void Start()
    {
        // load the neural network model from the asset:
        Model model = ModelLoader.Load(onnxModel);
        // create the neural network engine:
        engine = WorkerFactory.CreateWorker(backendType, model);

        // CreateOps allows direct operations on tensors.
        ops = WorkerFactory.CreateOps(backendType, null);

        //The camera which we'll be using to calculate the rays on the image:
        // lookCamera = Camera.main;
    }

    // Sends the image to the neural network model and returns the probability that the image is each particular digit.
    public Texture2D Execute(Texture2D drawableTexture)
    {
        inputTensor?.Dispose();

        // Convert the texture into a tensor, it has width=W, height=W, and channels=3:    
        inputTensor = TextureConverter.ToTensor(drawableTexture, imageWidth, imageWidth, 3);
        
        // Run the neural network:
        engine.Execute(inputTensor);
        
        // We get a reference to the output of the neural network while keeping it on the GPU
        TensorFloat result = engine.PeekOutput() as TensorFloat;

        // Divide the output values by 255, to remap to the (0-1) color range
        result = ops.Div(result, 255f);
        
        // Move a tensor on the GPU to the CPU
        result.MakeReadable();
        result.PrintDataPart(10);

        RenderTexture converted = TextureConverter.ToTexture(result);
        Texture2D resultImage = toTexture2D(converted);

        return resultImage;
        // // convert the result to probabilities between 0..1 using the softmax function:
        // var probabilities = ops.Softmax(result);
        // var indexOfMaxProba = ops.ArgMax(probabilities, -1, false);
        
        // // We need to make the result from the GPU readable on the CPU
        // probabilities.MakeReadable();
        // indexOfMaxProba.MakeReadable();

        // var predictedNumber = indexOfMaxProba[0];
        // var probability = probabilities[predictedNumber];

        // return (probability, predictedNumber);
    }

    void FixedUpdate()
    {
        i += 1;
        if (i % 2 != 0) return;

        // if (Input.GetMouseButtonDown(0))
        // {
            // MouseClicked();
            Debug.Log("MouseClicked");


            Texture2D image =  GetCameraImage();

            
            // テクスチャの変更
            // display.SetTexture("_MainTex", image);


            Texture2D resultImage = Execute(image);
            Debug.Log(resultImage.width.ToString() + ", " + resultImage.height.ToString());

            // テクスチャの変更
            // display.SetTexture("_MainTex", image);
            display.SetTexture("_BaseMap", resultImage);

        // }
        // else if (Input.GetMouseButton(0))
        // {
        //     // MouseIsDown();
        //     // Debug.Log("MouseIsDown");
        // }

        Resources.UnloadUnusedAssets();
    }

    // Detect the mouse click and send the info to the panel class
    // void MouseClicked()
    // {
    //     Ray ray = lookCamera.ScreenPointToRay(Input.mousePosition);
    //     if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.name == "Screen")
    //     {
    //         Panel panel = hit.collider.GetComponentInParent<Panel>();
    //         if (!panel) return;
    //         panel.ScreenMouseDown(hit);
    //     }
    // }

    // // Detect if the mouse is down and sent the info to the panel class
    // void MouseIsDown()
    // {
    //     Ray ray = lookCamera.ScreenPointToRay(Input.mousePosition);
    //     if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.name == "Screen")
    //     {
    //         Panel panel = hit.collider.GetComponentInParent<Panel>();
    //         if (!panel) return;
    //         panel.ScreenGetMouse(hit);
    //     }
    // }
   
    // Clean up all our resources at the end of the session so we don't leave anything on the GPU or in memory:
    private void OnDestroy()
    {
        inputTensor?.Dispose();
        engine?.Dispose();
        ops?.Dispose();
    }

    private Texture2D GetCameraImage()
	{
        // Game 画面のサイズを取得
        var size = new Vector2Int((int)Handles.GetMainGameViewSize().x, (int)Handles.GetMainGameViewSize().y);
        var render = new RenderTexture(size.x, size.y, 24);
        var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        // var cemara = Camera.main;
        var cemara = lookCamera;

        try
        {
            // カメラ画像を RenderTexture に描画
            cemara.targetTexture = render;
            cemara.Render();

            // RenderTexture の画像を読み取る
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            texture.Apply();
        }
        finally
        {
            cemara.targetTexture = null;
            RenderTexture.active = null;
        }
		return texture;
	}

    Texture2D toTexture2D(RenderTexture rTex)
    {
        // var size = new Vector2Int((int)Handles.GetMainGameViewSize().x, (int)Handles.GetMainGameViewSize().y);
        Texture2D tex = new Texture2D(imageWidth, imageWidth, TextureFormat.RGB24, false);
        // ReadPixels looks at the active RenderTexture.
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

}
