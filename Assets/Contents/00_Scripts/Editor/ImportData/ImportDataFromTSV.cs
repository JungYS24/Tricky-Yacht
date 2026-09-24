using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public abstract class ImportDataFromTSV : EditorWindow
{
    protected string tsvPath;
    protected string importDirectory;
    protected string dataDirectory;

    protected virtual void OnGUI()
    {
        DrawTSVPathField();
        DrawImportDirectoryField();
        DrawDatasDirectoryField();
        if (GUILayout.Button("Import"))
        {
            ImportData();
        }
    }

    /// <summary>
    /// tsv 파일 경로와 찾는 버튼을 그려줍니다.
    /// </summary>
    protected virtual void DrawTSVPathField()
    {
        tsvPath = GUILayout.TextArea(tsvPath);
        if (GUILayout.Button("Select TSV File"))
        {
            var path = EditorUtility.OpenFilePanel(
                "Select .tsv File",
                "Assets",
                "tsv"
            );

            if (!string.IsNullOrEmpty(path))
            {
                tsvPath = path;
            }
        }
    }

    /// <summary>
    /// 에셋을 저장할 위치와 찾는 버튼을 그려줍니다.
    /// </summary>
    protected virtual void DrawImportDirectoryField()
    {
        importDirectory = GUILayout.TextArea(importDirectory);
        if (GUILayout.Button("Select Import Directory"))
        {
            var dir = EditorUtility.OpenFolderPanel(
                "Select Import Target Folder",
                "Assets",
                "Datas"
            );

            if (!string.IsNullOrEmpty(dir))
            {
                importDirectory = dir;
            }
        }
    }

    /// <summary>
    /// 참조할 데이터들의 경로와 찾는 버튼을 그려줍니다.
    /// </summary>
    protected virtual void DrawDatasDirectoryField()
    {
        dataDirectory = GUILayout.TextArea(dataDirectory);
        if (GUILayout.Button("Select Datas Folder"))
        {
            var dir = EditorUtility.OpenFolderPanel(
                "Select Datas Folder",
                "Assets",
                "Datas"
            );

            if (!string.IsNullOrEmpty(dir))
            {
                dataDirectory = FileUtil.GetProjectRelativePath(dir);
            }
        }
    }
    
    /// <summary>
    /// 입력된 파일 경로를 통해 순차적으로 읽어들여 파일을 해석합니다.
    /// 
    /// </summary>
    /// <returns>시트의 키 값에 해당하는 열의 값들.</returns>
    protected Dictionary<string, List<string>> Parse()
    {
        try
        {
            Dictionary<string, List<string>> dict = new();

            using StreamReader sr = new(tsvPath);
            var headers = sr.ReadLine().Split('\t');
            foreach (var header in headers)
            {
                dict[header] = new();
            }

            int len = headers.Length;
            while (!sr.EndOfStream)
            {
                var data = sr.ReadLine().Split('\t');
                for (int i = 0; i < len; i++)
                {
                    dict[headers[i]].Add(data[i]);
                }
            }

            return dict;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    /// <summary>
    /// 파싱된 데이터를 바탕으로 데이터를 저장합니다.
    /// </summary>
    protected abstract void ImportData();
}