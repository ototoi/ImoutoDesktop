﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

using ImoutoDesktop.Remoting;

namespace ImoutoDesktop.Server
{
    public class RemoteService : MarshalByRefObject, IRemoteService
    {
        private bool isLogined;

        public bool Login(string password)
        {
            // ログインする
            var md5 = new MD5CryptoServiceProvider();
            var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(Settings.Default.Password));
            var masterPassword = BitConverter.ToString(hash).Replace("-", "").ToLower();
            if (masterPassword == password)
            {
                isLogined = true;
                return true;
            }
            return false;
        }

        public bool IsConnecting
        {
            get { return true; }
        }

        private object syncLock = new object();

        public FileStream OpenFile(string path, FileMode mode)
        {
            if (!isLogined)
            {
                return null;
            }
            try
            {
                return File.Open(path, mode);
            }
            catch
            {
                return null;
            }
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            if (!isLogined)
            {
                return null;
            }
            return Directory.GetFiles(path, searchPattern);
        }

        public string[] GetDirectories(string path, string searchPattern)
        {
            if (!isLogined)
            {
                return null;
            }
            return Directory.GetDirectories(path, searchPattern);
        }

        public bool DeleteFile(string path)
        {
            if (!isLogined)
            {
                return false;
            }
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool CopyFile(string sourcePath, string destPath)
        {
            if (!isLogined)
            {
                return false;
            }
            try
            {
                File.Copy(sourcePath, destPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool MoveFile(string sourcePath, string destPath)
        {
            if (!isLogined)
            {
                return false;
            }
            try
            {
                File.Move(sourcePath, destPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetFolderPath(Environment.SpecialFolder folder)
        {
            if (!isLogined)
            {
                return null;
            }
            return Environment.GetFolderPath(folder);
        }

        private delegate object RemoteInvoker();

        public string ExecuteCommand(string command)
        {
            if (!isLogined)
            {
                return null;
            }
            return (string)Form1.Form.Invoke((RemoteInvoker)delegate
            {
                var psi = new ProcessStartInfo();
                psi.FileName = Environment.GetEnvironmentVariable("ComSpec");
                psi.RedirectStandardInput = false;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Environment.CurrentDirectory;
                psi.Arguments = $"/c {command}";
                var process = Process.Start(psi);
                var result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            });
        }

        private Dictionary<string, Process> processes = new Dictionary<string, Process>();

        public bool ExecuteProcess(string fileName, string argument)
        {
            if (!isLogined)
            {
                return false;
            }
            try
            {
                var process = (Process)Form1.Form.Invoke((RemoteInvoker)delegate
                {
                    return Process.Start(fileName, argument);
                });
                lock (syncLock)
                {
                    if (process != null)
                    {
                        process.EnableRaisingEvents = true;
                        process.Exited += new EventHandler(Process_Exited);
                        process.SynchronizingObject = Form1.Form;
                        if (!processes.ContainsKey(fileName))
                        {
                            processes.Add(fileName, process);
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            lock (syncLock)
            {
                string key = null;
                foreach (var item in processes)
                {
                    if (item.Value == (Process)sender)
                    {
                        key = item.Key;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }
                processes.Remove(key);
            }
        }

        public bool CloseProcess(string name)
        {
            if (!isLogined)
            {
                return false;
            }
            lock (syncLock)
            {
                foreach (var item in processes)
                {
                    try
                    {
                        if (item.Value.HasExited)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    if (item.Value.ProcessName == name || item.Key == name)
                    {
                        if (!item.Value.CloseMainWindow())
                        {
                            item.Value.Kill();
                        }
                        return true;
                    }
                }
                var result = false;
                foreach (var item in Process.GetProcessesByName(name))
                {
                    item.Kill();
                    result = true;
                }
                return result;
            }
        }

        public string CurrentDirectory
        {
            get
            {
                if (!isLogined)
                {
                    return null;
                }
                return Environment.CurrentDirectory;
            }
            set
            {
                if (!isLogined)
                {
                    return;
                }
                Environment.CurrentDirectory = value;
            }
        }

        public void Shutdown()
        {
            if (!isLogined)
            {
                return;
            }
            NativeMethods.ExitWindows(NativeMethods.Shutdown);
        }

        public ImoutoDesktop.Remoting.Exists Exists(string path)
        {
            if (!isLogined)
            {
                return ImoutoDesktop.Remoting.Exists.None;
            }
            if (File.Exists(path))
            {
                return ImoutoDesktop.Remoting.Exists.File;
            }
            else if (Directory.Exists(path))
            {
                return ImoutoDesktop.Remoting.Exists.Directory;
            }
            else
            {
                return ImoutoDesktop.Remoting.Exists.None;
            }
        }

        public Stream GetScreenshot(int width)
        {
            if (!isLogined)
            {
                return null;
            }
            var rate = (double)Screen.PrimaryScreen.Bounds.Height / (double)Screen.PrimaryScreen.Bounds.Width;
            var temp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            var g = Graphics.FromImage(temp);
            g.CopyFromScreen(new Point(0, 0), new Point(0, 0), temp.Size);
            g.Dispose();
            var bitmap = new Bitmap(width, (int)(rate * width));
            g = Graphics.FromImage(bitmap);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(temp, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, temp.Width, temp.Height, GraphicsUnit.Pixel);
            g.Dispose();
            temp.Dispose();
            var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream;
        }

        private static readonly Dictionary<string, DirectoryType> ext2type = new Dictionary<string, DirectoryType>()
        {
            { ".bmp", DirectoryType.Picture }, { ".png", DirectoryType.Picture },
            { ".gif", DirectoryType.Picture }, { ".jpg", DirectoryType.Picture },
            { ".jpeg", DirectoryType.Picture },
            { ".mpg", DirectoryType.Movie }, { ".mpeg", DirectoryType.Movie },
            { ".flv", DirectoryType.Movie }, { ".avi", DirectoryType.Movie },
            { ".wmv", DirectoryType.Movie },
            { ".mp3", DirectoryType.Music }, { ".m4a", DirectoryType.Music },
            { ".m4p", DirectoryType.Music }, { ".ogg", DirectoryType.Music },
            { ".wav", DirectoryType.Music }, { ".mid", DirectoryType.Music },
            { ".wma", DirectoryType.Music },
            { ".txt", DirectoryType.Document }, { ".rtf", DirectoryType.Document },
            { ".doc", DirectoryType.Document }, { ".docx", DirectoryType.Document }
        };

        public DirectoryType GetDirectoryType(string directory)
        {
            if (!isLogined)
            {
                return DirectoryType.None;
            }
            if (!Directory.Exists(directory))
            {
                return DirectoryType.None;
            }
            var count = 0;
            var filecount = 0;
            var types = new Dictionary<DirectoryType, int>();
            foreach (var item in Directory.GetFiles(directory))
            {
                DirectoryType type;
                if (ext2type.TryGetValue(Path.GetExtension(item).ToLower(), out type))
                {
                    if (!types.ContainsKey(type))
                    {
                        types.Add(type, 1);
                    }
                    else
                    {
                        types[type] += 1;
                    }
                    count++;
                }
                filecount++;
            }
            if (filecount == 0)
            {
                var temp = Directory.GetDirectories(directory);
                if (temp.Length == 0)
                {
                    return DirectoryType.Empty;
                }
                else
                {
                    return DirectoryType.Mixed;
                }
            }
            else if (count < 10 || types.Count == 0)
            {
                return DirectoryType.Mixed;
            }
            else
            {
                var values = new List<KeyValuePair<int, DirectoryType>>();
                foreach (var item in types)
                {
                    values.Add(new KeyValuePair<int, DirectoryType>(item.Value, item.Key));
                }
                values.Sort(delegate (KeyValuePair<int, DirectoryType> left, KeyValuePair<int, DirectoryType> right) { return Comparer<int>.Default.Compare(right.Key, left.Key); });
                return values[0].Value;
            }
        }
    }
}