using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class AutoBackup : MonoBehaviour
{
    private MonoBehaviour DesignerScript;
    private Type DesignerScriptType;

    private MethodInfo OnFocusInfo;

    private float AutosaveInterval = 300f;
    private float TimeToNextAutosave;
    private bool WaitingToCopy = false;

    private bool AutoBackupEnabled = true;
    private int AutoBackupInterval = 1;
    private int CyclesToNextBackup;

    private string EditorPath;
    private string PreviousAircraftData;

    private string NachSaveFolder = "NACHSAVE";
    private string ModDataFolder = "AUTOBAK";
    private string ModDataPath;

    private string IntervalName = "INTERVAL.TXT";
    private string IntervalPath;

    private void Start()
    {
        // Commands
        ServiceProvider.Instance.DevConsole.RegisterCommand("BackupAircraft", TryMakeBackup);

        MonoBehaviour[] allBehaviors = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour c in allBehaviors)
        {
            if (c.GetType().FullName == "Assets.Scripts.Designer.DesignerScript")
            {
                DesignerScript = c;
                DesignerScriptType = c.GetType();
                Debug.Log("Found DesignerScript: " + DesignerScript.ToString());
                break;
            }
        }

        if (DesignerScript == null)
        {
            Debug.LogError("Cannot find DesignerScript!");
        }
        else
        {
            OnFocusInfo = DesignerScriptType.GetMethod("OnApplicationFocus",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        }

        EditorPath = Path.Combine(Application.persistentDataPath, "AircraftDesigns", "__editor__.xml");

        ModDataPath = Path.Combine(Application.persistentDataPath, NachSaveFolder, ModDataFolder);
        Directory.CreateDirectory(ModDataPath);

        IntervalPath = Path.Combine(ModDataPath, IntervalName);
        if (File.Exists(IntervalPath))
        {
            TryReadIntervalFile();
        }
        else
        {
            WriteIntervalFile(AutosaveInterval, AutoBackupInterval);
            Debug.LogWarning("Settings file not found. New settings file created. Initialized with default values.");
        }

        // Prevent users having too small a backup interval
        if (AutosaveInterval < 15f) AutosaveInterval = 15f;

        Debug.Log($"Loaded settings file.\nAutosave every {AutosaveInterval} seconds.\nBack up every {AutoBackupInterval} cycles.");
        TimeToNextAutosave = AutosaveInterval;
        CyclesToNextBackup = AutoBackupInterval;

        AutoBackupEnabled = AutoBackupInterval >= 1;
        if (AutoBackupEnabled)
            TryMakeBackup();
    }

    private void Update()
    {
        if (DesignerScript == null)
        {
            return;
        }

        TimeToNextAutosave -= Time.unscaledDeltaTime;
        if (TimeToNextAutosave <= 0f)
        {
            // Causes the DesignerScript to save to __editor__.xml
            OnFocusInfo.Invoke(DesignerScript, new object[] { true });
            WaitingToCopy = AutoBackupEnabled;
            TimeToNextAutosave = AutosaveInterval;

            Debug.Log("Autosaved to editor file.");
        }

        if (WaitingToCopy)
        {
            if (CyclesToNextBackup > 1)
            {
                CyclesToNextBackup--;
                WaitingToCopy = false;
            }
            else // == 1
            {
                WaitingToCopy = !TryMakeBackup();
                CyclesToNextBackup = AutoBackupInterval;
            }
        }
    }

    private bool TryMakeBackup()
    {
        string editorAircraftData;
        string backupName = string.Format("backup{0}{1:D2}{2:D2}{3:D2}{4:D2}{5:D2}.xml",
            DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
            DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);

        try
        {
            editorAircraftData = File.ReadAllText(EditorPath);
            if (editorAircraftData != PreviousAircraftData)
            {
                File.WriteAllText(Path.Combine(ModDataPath, backupName), editorAircraftData, System.Text.Encoding.UTF8);
                Debug.Log($"Backup written: {backupName}");
            }
            else
            {
                Debug.Log("No backup written: Current aircraft data is the same as the previous data in the session.");
            }
        }
        catch (IOException ex)
        {
            Debug.LogWarning("Cannot make a backup now: " + ex.Message);
            return false;    // Failed
        }

        // Success
        PreviousAircraftData = editorAircraftData;
        return true;
    }

    private void TryReadIntervalFile()
    {
        using (StreamReader sr = new StreamReader(File.OpenRead(IntervalPath)))
        {
            try
            {
                AutosaveInterval = float.Parse(sr.ReadLine());
                AutoBackupInterval = int.Parse(sr.ReadLine());
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                Debug.LogWarning("Exception when loading settings file. Please delete or repair settings file. Initialized with default values.");
            }
        }
    }

    private void WriteIntervalFile(float autosave, int backup)
    {
        using (StreamWriter sw = File.CreateText(IntervalPath))
        {
            sw.WriteLine(autosave);
            sw.WriteLine(backup);
            sw.WriteLine("\n* In the first line, enter the autosave period in seconds [float].");
            sw.WriteLine("    - The minimum period is 15 seconds. Smaller numbers are increased to the minimum.");
            sw.WriteLine("\n* In the second line, enter the backup period in autosave cycles [int].");
            sw.WriteLine("    - Zero or a negative number will disable auto backups.");
            sw.WriteLine("\n* Close this file before entering the designer.");
            sw.WriteLine("\n* Delete this file and enter the designer to restore default settings.");
        }
    }

    private void OnDisable()
    {
        ServiceProvider.Instance.DevConsole.UnregisterCommand("BackupAircraft");
    }
}
