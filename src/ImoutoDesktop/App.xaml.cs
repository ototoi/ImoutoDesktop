﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Reflection;

using ImoutoDesktop.IO;
using ImoutoDesktop.Commands;

namespace ImoutoDesktop
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            if (!_mutex.WaitOne(0, false))
            {
                MessageBox.Show("既に起動しています");
                Shutdown();
                return;
            }
#if DEBUG
            RootDirectory = @"C:\Users\しばやん\Documents\Visual Studio 2008\Projects\ImoutoDesktop";
#else
            var assembly = Assembly.GetEntryAssembly();
            RootDirectory = Path.GetDirectoryName(assembly.Location);
#endif
            // 設定ファイルを読み込む
            Settings.Load(Path.Combine(RootDirectory, "settings.xml"));
            // インストールされているいもうとを読み込む
            CharacterManager.Rebuild(Path.Combine(RootDirectory, "characters"));
            // インストールされているバルーンを読み込む
            BalloonManager.Rebuild(Path.Combine(RootDirectory, "balloons"));
            // コマンドライブラリを読み込む
            CommandManager.Rebuild(Path.Combine(RootDirectory, "commands"));
            // テンポラリディレクトリを作成
            if (!Directory.Exists(Path.Combine(RootDirectory, "temp")))
            {
                Directory.CreateDirectory(Path.Combine(RootDirectory, "temp"));
            }
            // 起動条件を満たしているか確認する
            if (CharacterManager.Characters.Count == 0 || BalloonManager.Balloons.Count == 0)
            {
                // いもうと、バルーンが存在しない
                MessageBox.Show("いもうと、バルーンがインストールされていません。");
                // シャットダウン
                Shutdown();
                return;
            }
            // コンテキストを作成して、いもうとを起動
            Context context = null;
            if (Settings.Default.LastCharacter.HasValue)
            {
                context = Context.Create(Settings.Default.LastCharacter.Value);
            }
            if (context == null)
            {
                // デフォルト「さくら」がいるか確認する
                context = Context.Create(_default);
                if (context == null)
                {
                    context = Context.Create(CharacterManager.Characters.ElementAt(0).Key);
                }
            }
            context.Run();
        }

        private static readonly Mutex _mutex = new Mutex(false, "ImoutoDesktop");
        private static readonly Guid _default = new Guid("{F3EC60A3-C5FB-443a-B05E-C3345AB37269}");

        public string RootDirectory { get; private set; }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            CommandManager.Shutdown();
            if (!string.IsNullOrEmpty(RootDirectory))
            {
                Settings.Save(Path.Combine(RootDirectory, "settings.xml"));
            }
            _mutex.Close();
        }
    }
}
