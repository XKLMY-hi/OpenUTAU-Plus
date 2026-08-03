using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.App.ViewModels {

    /// <summary>侧栏歌手卡片：歌手 + 懒加载头像 + 引擎铭牌（仅 Classic/DiffSinger）。</summary>
    public class SingerItem {
        public USinger Singer { get; }
        public string Name => Singer.LocalizedName;
        public bool ShowBadge => Singer.SingerType is USingerType.Classic or USingerType.DiffSinger;
        public string BadgeText => Singer.SingerType == USingerType.DiffSinger ? "DiffSinger" : "Classic";

        private Bitmap? avatar;
        public Bitmap? Avatar => avatar ??= LoadAvatar(Singer);

        public SingerItem(USinger singer) {
            Singer = singer;
        }

        private static Bitmap? LoadAvatar(USinger singer) {
            if (singer.AvatarData == null) {
                return null;
            }
            try {
                using (var stream = new MemoryStream(singer.AvatarData)) {
                    return new Bitmap(stream);
                }
            } catch (Exception e) {
                Log.Error(e, "Failed to load avatar.");
                return null;
            }
        }
    }

    /// <summary>侧栏伴奏卡片：仅文件名 + 路径（不读元数据，试听/添加时才读取）。</summary>
    public class SampleItem {
        public string Name { get; }
        public string Path { get; }

        public SampleItem(string path) {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
        }
    }

    /// <summary>
    /// 侧栏素材库 VM（阶段 E4）：歌手列表 + 伴奏库。
    /// - 歌手：SingerManager 扁平列表，订阅 SingersRefreshedNotification 自动刷新
    /// - 伴奏：枚举 SampleSearchPaths + SamplesPath 内音频格式文件（不读元数据）
    /// </summary>
    public class SidebarViewModel : ViewModelBase, ICmdSubscriber {
        static readonly string[] SampleExts = { ".wav", ".mp3", ".ogg", ".opus", ".flac" };

        public ObservableCollection<SingerItem> Singers { get; } = new ObservableCollection<SingerItem>();
        public ObservableCollection<SampleItem> Samples { get; } = new ObservableCollection<SampleItem>();

        public SidebarViewModel() {
            DocManager.Inst.AddSubscriber(this);
            RefreshSingers();
            RefreshSamples();
        }

        public void RefreshSingers() {
            var singers = SingerManager.Inst.SingerGroups.Values
                .SelectMany(list => list)
                .ToList();
            Singers.Clear();
            foreach (var singer in singers) {
                Singers.Add(new SingerItem(singer));
            }
        }

        /// <summary>重扫伴奏目录：仅枚举文件名（性能：不读时长/元数据）。</summary>
        public void RefreshSamples() {
            var files = new List<string>();
            foreach (var dir in PathManager.Inst.SamplesPaths) {
                try {
                    foreach (var file in Directory.GetFiles(dir)) {
                        if (SampleExts.Contains(System.IO.Path.GetExtension(file).ToLower())) {
                            files.Add(file);
                        }
                    }
                } catch (Exception e) {
                    Log.Error(e, $"Failed to scan samples directory {dir}");
                }
            }
            Samples.Clear();
            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) {
                Samples.Add(new SampleItem(file));
            }
        }

        public void OnNext(UCommand cmd, bool isUndo) {
            if (cmd is SingersRefreshedNotification) {
                RefreshSingers();
            }
        }
    }
}
