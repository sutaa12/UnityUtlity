using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditorWindowでGameViewに好きな画像を重ねられるスクリプト
/// PNG画像のみ対応
/// </summary>
public class ImageOverlapGameViewWindow : EditorWindow
{
    static readonly int SubTex = Shader.PropertyToID("_SubTex");
    static readonly int TexAlpha = Shader.PropertyToID("_TexAlpha");

    string[] paths = new string[0];
    RenderTexture behiviorTexture;
    Texture2D texture;
    Material material;
    float imageAlpha = 0.5f;
    Vector2 oldSize;
    Vector2 scrollPos = Vector2.zero;
    bool isSnapWindow = true;
    bool isReadPng;

    [MenuItem("Custom Tools/Common/ImageOverlapWindow(画像をGameViewに重ねられるくん)")]
    static void Open()
    {
        GetWindow<ImageOverlapGameViewWindow>(true, "ImageOverlapWindow");
    }

    void GetTextureForPng(string path)
    {
        if (!path.Contains(".png"))
        {
            Debug.Log("pngじゃないので読めませんでした!");
            return;
        }

        var readBinary = ReadPngFile(path);

        var pos = 16; // 16バイトから開始

        var width = 0;
        for (int i = 0; i < 4; i++)
        {
            width = width * 256 + readBinary[pos++];
        }

        var height = 0;
        for (int i = 0; i < 4; i++)
        {
            height = height * 256 + readBinary[pos++];
        }

        texture = new Texture2D(width, height);
        texture.LoadImage(readBinary);
        isReadPng = true;
    }

    byte[] ReadPngFile(string path)
    {
        using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var bin = new BinaryReader(fileStream))
        {
            var values = bin.ReadBytes((int)bin.BaseStream.Length);
            return values;
        }
    }

    RenderTexture GetGameTexture()
    {
        var gameMainView = GetMainGameView();
        if (gameMainView == null)
        {
            return null;
        }
        

        var t = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (t == null) return null;
        var renderTexture = t.GetField("m_RenderTexture",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.FlattenHierarchy |
            BindingFlags.SetField);
        if (renderTexture == null)
        {
            return null;
        }

        return (RenderTexture) renderTexture.GetValue(gameMainView);

    }

    public static EditorWindow GetMainGameView()
    {
        PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
        var t = System.Type.GetType("UnityEditor.PlayModeView,UnityEditor");
        if (t == null) return null;
        var getMainGameView = t.GetMethod("GetMainPlayModeView", BindingFlags.NonPublic | BindingFlags.Static);
        if (getMainGameView == null) return null;
        var res = getMainGameView.Invoke(null, null) ?? GetWindow(t);
        return (EditorWindow)res;

    }

    void OnFocus()
    {
        material = new Material(Shader.Find("EDITOR/EditorOverlayImage"));
    }

    // 毎フレーム更新
    void Update()
    {
        Repaint();
    }

    void SetupWindow()
    {
        if (material == null)
        {
            material = new Material(Shader.Find("EDITOR/EditorOverlayImage"));
        }

        material.SetTexture(SubTex, texture);
        material.SetFloat(TexAlpha, imageAlpha);
        behiviorTexture = GetGameTexture();
        SetupSize();
    }

    void SetupSize()
    {
        var size = Handles.GetMainGameViewSize();
        var currentSize = GetMainGameView().position.size;
        var scale = 0.0f;
        if (size.y / size.x > currentSize.y / currentSize.x)
        {
            scale = currentSize.y / size.y;
        } else
        {
            scale = currentSize.x / size.x;
        }

        size *= scale;
        var pos = GetMainGameView().position.center - size / 2;
        pos.y += 50;
        if (isSnapWindow)
        {
            position = new Rect(pos, size);
        }

        minSize = size;
        maxSize = size;
    }

    void OnGUI()
    {
        SetupWindow();

        var evt = Event.current;
        var width = position.size.x;
        var height = position.size.y;
        var dropArea = new Rect(position.width / 2 - width / 2, position.height / 2 - height / 2, width, height);

        if (behiviorTexture != null)
        {
            EditorGUI.DrawPreviewTexture(dropArea, behiviorTexture, material);
        }

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition)) break;

                //マウスの形状
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    paths = DragAndDrop.paths;
                    DragAndDrop.activeControlID = 0;
                }

                if (paths.Length > 0 && !String.IsNullOrEmpty(paths[0]))
                {
                    GetTextureForPng(paths[0]);
                }

                Event.current.Use();
                break;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUIStyle.none, GUIStyle.none);
        {
            if (!isReadPng)
            {
                GUILayout.TextField("png画像をドラッグ&ドロップしてください");
                GUILayout.TextField("下にスクロールすると透明度の設定が出ます");
            }

            GUILayout.Space(height + 50);
            GUILayout.TextField("透明度設定");
            imageAlpha = GUILayout.HorizontalSlider(imageAlpha, 0, 1);
            GUILayout.Space(20);
        }
        isSnapWindow = GUILayout.Toggle(isSnapWindow, "画面にくっつけるかどうか");
        GUILayout.TextField("読み込みはPNGのみ対応 GameWindowを必ず開いておいてください");
        EditorGUILayout.EndScrollView();
    }
}
