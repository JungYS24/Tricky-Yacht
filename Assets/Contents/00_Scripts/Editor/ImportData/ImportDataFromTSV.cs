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
                dataDirectory = dir;
            }
        }
    }
    
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

    protected abstract void ImportData();
}