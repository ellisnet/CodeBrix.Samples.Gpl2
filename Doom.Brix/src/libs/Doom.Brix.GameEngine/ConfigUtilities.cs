//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagedDoom
{
    public static class ConfigUtilities
    {
        private static readonly string[] iwadNames = new string[]
        {
            // RE-ENABLE ADDITIONAL .WAD FILE SUPPORT HERE
            //"DOOM2.WAD",
            //"PLUTONIA.WAD",
            //"TNT.WAD",
            //"DOOM.WAD",
            "DOOM1.WAD"
            // RE-ENABLE ADDITIONAL .WAD FILE SUPPORT HERE
            //, "FREEDOOM2.WAD"
            //, "FREEDOOM1.WAD"
        };

        private static string dataDirectory;
        private static IDoomStorage storage;

        // The persistence seam for config and save slots. Defaults to the
        // file-based storage (managed-doom's original behavior, under
        // DataDirectory) so the headless engine and its tests need no setup;
        // the hosting application assigns a settings.sqlite-backed
        // implementation before booting the game, exactly like DataDirectory.
        public static IDoomStorage Storage
        {
            get => storage ??= new FileDoomStorage();
            set => storage = value;
        }

        // The single funnel for every disk path the game touches: the config
        // file, DOOM1.WAD discovery and the save-game slots all resolve
        // against this directory. The hosting application sets this to its
        // game-assets folder before booting the game; if it is never set,
        // the application's own directory is used.
        // (This replaces the original GetExeDirectory(), which anchored all
        // of those paths to the folder containing the executable.)
        public static string DataDirectory
        {
            get
            {
                if (dataDirectory == null)
                {
                    dataDirectory = AppContext.BaseDirectory;
                }

                return dataDirectory;
            }

            set
            {
                dataDirectory = value;
            }
        }

        public static string GetConfigPath()
        {
            return Path.Combine(DataDirectory, "managed-doom.cfg");
        }

        public static string GetDefaultIwadPath()
        {
            var dataDirectory = DataDirectory;
            foreach (var name in iwadNames)
            {
                var path = Path.Combine(dataDirectory, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            foreach (var name in iwadNames)
            {
                var path = Path.Combine(currentDirectory, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new Exception("No IWAD was found!");
        }

        public static bool IsIwad(string path)
        {
            var name = Path.GetFileName(path).ToUpper();
            return iwadNames.Contains(name);
        }

        public static string[] GetWadPaths(CommandLineArgs args)
        {
            var wadPaths = new List<string>();

            if (args.iwad.Present)
            {
                wadPaths.Add(args.iwad.Value);
            }
            else
            {
                wadPaths.Add(ConfigUtilities.GetDefaultIwadPath());
            }

            if (args.file.Present)
            {
                foreach (var path in args.file.Value)
                {
                    wadPaths.Add(path);
                }
            }

            return wadPaths.ToArray();
        }
    }
}
