﻿using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading;
using FileDemo_1734.Class;
using System.Collections.Generic;
using System.Linq;

namespace FileDemo_1734
{
    public class Program
    {
        /// <summary>
        /// 儲存檔案快照
        /// </summary>
        private static ConcurrentDictionary<string, List<string>> FileContentSnapshots = new ConcurrentDictionary<string, List<string>>();
        /// <summary>
        /// 定時器
        /// </summary>
        private static Timer checkFilesTimer;
        /// <summary>
        /// 5秒檢查一次檔案
        /// </summary>
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        static void Main(string[] args)
        {
            //讀取config.json，並將config.json反序列化成config物件
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            //設定監控位置。使用@來避免使用跳脫字符
            string monitorDirectory = @"C:\temp\TEST";
            config.DirectoryPath = monitorDirectory;

            //檢查並建立目錄以及檔案
            FolderFileCreate(monitorDirectory, config.FilesToMonitor);

            Console.WriteLine($"正在監控目錄:{config.DirectoryPath}");
            DisplayMonitoredFiles(config.FilesToMonitor);

            //初始化每個檔案的快照
            foreach (var file in config.FilesToMonitor)
            {
                //使用combine組合路徑
                string filePath = Path.Combine(config.DirectoryPath, file);

                //file.Exists用來檢查指定的檔案路徑是否存在
                if (File.Exists(filePath))
                {
                    // 將每個檔案逐行讀取並儲存，以避免載入整個檔案到記憶體
                    FileContentSnapshots[filePath] = ReadFileLines(filePath);
                }
                else
                {
                    FileContentSnapshots[filePath] = new List<string>();
                }
            }

            //啟動定時器，每隔CheckInterval所設定的秒數檢查一次檔案
            checkFilesTimer = new Timer(CheckFileChange, config, TimeSpan.Zero, CheckInterval);

            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ;

            checkFilesTimer?.Dispose();
        }

        private static void FolderFileCreate(string directoryPath, string[] filesToMonitor)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"已建立目錄:{directoryPath}");
            }

            foreach (var file in filesToMonitor)
            {
                string filePath = Path.Combine(directoryPath, file);
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose();
                    Console.WriteLine($"已建立檔案:{filePath}");
                }
            }
        }

        private static void DisplayMonitoredFiles(string[] fileToMonitor)
        {
            foreach (var file in fileToMonitor)
            {
                Console.WriteLine($"正在監控檔案:{file}");
            }
        }


        // 新增方法，用於逐行讀取檔案，減少記憶體佔用
        //使用 while 迴圈來逐行讀取檔案，reader.ReadLine() 方法會讀取檔案中的一行並將其賦值給 line。
        private static List<string> ReadFileLines(string filePath)
        {
            var lines = new List<string>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
                return lines;
        }

        private static void CheckFileChange(object state)
        {
            try
            {
                Config config = (Config)state;

                foreach (var file in config.FilesToMonitor)
                {
                    string filePath = Path.Combine(config.DirectoryPath, file);

                    //使用File.Exists 方法來檢查 filePath 所指向的檔案是否存在
                    if (File.Exists(filePath))
                    {
                        Console.WriteLine($"正在檢查檔案: {filePath}");

                        // 改用逐行讀取檔案來避免過大的記憶體佔用
                        var newContent = ReadFileLines(filePath);
                        var oldContent = FileContentSnapshots.GetOrAdd(filePath, new List<string>());

                        //HashSet<>用於查詢刪除插入具有較高的性能
                        var newContentSet = new HashSet<string>(newContent);
                        var oldContentSet = new HashSet<string>(oldContent);

                        // 找出新增的行(在newContentSet中存在但不在oldContentSet中)
                        foreach (var line in newContentSet.Except(oldContentSet))
                        {
                            Console.WriteLine($"新增的行: {line}");
                        }

                        // 找出修改的行
                        if (newContent.Count == oldContent.Count)
                        {
                            for (int j = 0; j < newContent.Count; j++)
                            {
                                if (newContent[j] != oldContent[j])
                                {
                                    Console.WriteLine($"修改的行: 原內容 - {oldContent[j]}, 新內容 - {newContent[j]}");
                                }
                            }
                        }

                        // 更新快照
                        FileContentSnapshots[filePath] = newContent;
                    }
                }

                // 清理快照以避免記憶體不足
                if (FileContentSnapshots.Count > 10)
                {
                    // 加入註解：移除過舊的快照以釋放記憶體
                    foreach (var key in FileContentSnapshots.Keys.Take(5))
                    {
                        FileContentSnapshots.TryRemove(key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"發生錯誤: {ex.Message}");
            }
        }
    }
}
