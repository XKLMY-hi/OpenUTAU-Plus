using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenUtau.Core.Ustx;
using Serilog;
using WanaKanaNet;

namespace OpenUtau.Classic {
    public class ClassicSinger : USinger {
        public override string Id => voicebank.Id;
        public override string Name => voicebank.Name;
        public override Dictionary<string, string> LocalizedNames => voicebank.LocalizedNames;
        public override USingerType SingerType => voicebank.SingerType;
        public override string BasePath => voicebank.BasePath;
        public override string Author => voicebank.Author;
        public override string Voice => voicebank.Voice;
        public override string Location => Path.GetDirectoryName(voicebank.File);
        public override string Web => voicebank.Web;
        public override string Version => voicebank.Version;
        public override string OtherInfo => voicebank.OtherInfo;
        public override IList<string> Errors => errors;
        // image 缺失时用 portrait 兜底（很多声库只声明 portrait）
        public override string Avatar {
            get {
                if (!string.IsNullOrEmpty(voicebank.Image)) {
                    return Path.Combine(Location, voicebank.Image);
                }
                if (!string.IsNullOrEmpty(voicebank.Portrait)) {
                    return Path.Combine(Location, voicebank.Portrait);
                }
                return null;
            }
        }
        public override byte[] AvatarData => avatarData;
        public override string Portrait => voicebank.Portrait == null ? null : Path.Combine(Location, voicebank.Portrait);
        public override float PortraitOpacity => voicebank.PortraitOpacity;
        public override int PortraitHeight => voicebank.PortraitHeight;
        public override string DefaultPhonemizer => voicebank.DefaultPhonemizer;
        public override string Sample => voicebank.Sample == null ? null : Path.Combine(Location, voicebank.Sample);
        public override Encoding TextFileEncoding => voicebank.TextFileEncoding;
        public override IList<USubbank> Subbanks => subbanks;
        public override IList<UOto> Otos => otos;

        Voicebank voicebank;
        List<string> errors = new List<string>();
        byte[] avatarData;
        List<UOtoSet> otoSets = new List<UOtoSet>();
        List<USubbank> subbanks = new List<USubbank>();
        List<UOto> otos = new List<UOto>();
        Dictionary<string, UOto> otoMap = new Dictionary<string, UOto>();
        OtoWatcher otoWatcher;

        public bool? UseFilenameAsAlias { get => voicebank.UseFilenameAsAlias; set => voicebank.UseFilenameAsAlias = value; }
        public Dictionary<string, IFrqFiles> Frqs { get; set; } = new Dictionary<string, IFrqFiles>();

        public ClassicSinger(Voicebank voicebank) {
            this.voicebank = voicebank;
            found = true;
        }

        public override void EnsureLoaded() {
            if (Loaded) {
                return;
            }
            Reload();
        }

        public override void Reload() {
            if (!Found) {
                return;
            }
            try {
                voicebank.Reload();
                Load();
                loaded = true;
                if (otoWatcher == null) {
                    otoWatcher = new OtoWatcher(this, Location);
                }
                OtoDirty = false;
            } catch (Exception e) {
                Log.Error(e, $"Failed to load {voicebank.File}");
            }
        }

        void Load() {
            if (Avatar != null && File.Exists(Avatar)) {
                try {
                    using (var stream = new FileStream(Avatar, FileMode.Open, FileAccess.Read)) {
                        using (var memoryStream = new MemoryStream()) {
                            stream.CopyTo(memoryStream);
                            avatarData = memoryStream.ToArray();
                        }
                    }
                } catch (Exception e) {
                    avatarData = null;
                    Log.Error(e, "Failed to load avatar data.");
                }
            } else {
                avatarData = null;
                Log.Error("Avatar can't be found");
            }

            subbanks.Clear();
            subbanks.AddRange(voicebank.Subbanks
                .OrderByDescending(subbank => subbank.Prefix.Length + subbank.Suffix.Length)
                .Select(subbank => new USubbank(subbank)));
            var groups = subbanks.GroupBy(subbank => $"^{Regex.Escape(subbank.Prefix)}(.*){Regex.Escape(subbank.Suffix)}$")
                .Select(group => new KeyValuePair<Regex, USubbank[]>(new Regex(group.Key), group.ToArray()));

            var dummy = new USubbank[] { new USubbank(new Subbank()) };
            otoSets.Clear();
            otos.Clear();
            otoMap.Clear();
            errors.Clear();
            
            foreach (var otoSet in voicebank.OtoSets) {
                var uSet = new UOtoSet(otoSet, voicebank.BasePath);
                otoSets.Add(uSet);
                foreach (var oto in otoSet.Otos) {
                    if (!oto.IsValid) {
                        if (!string.IsNullOrEmpty(oto.Error)) {
                            errors.Add(oto.Error);
                        }
                        continue;
                    }
                    UOto? uOto = null;
                    foreach (var group in groups) {
                        var m = group.Key.Match(oto.Alias);
                        if (m.Success) {
                            oto.Phonetic = m.Groups[1].Value;
                            uOto = new UOto(oto, uSet, group.Value);
                            break;
                        }
                    }
                    if (uOto == null) {
                        uOto = new UOto(oto, uSet, dummy);
                    }
                    otos.Add(uOto);
                    if (!otoMap.ContainsKey(oto.Alias)) {
                        otoMap.Add(oto.Alias, uOto);
                    } else {
                        //Errors.Add($"oto conflict {Otos[oto.Alias].Set}/{oto.Alias} and {otoSet.Name}/{oto.Alias}");
                    }
                }
            }

            Task.Run(() => {
                otoMap.Values
                    .ToList()
                    .ForEach(oto => {
                        oto.SearchTerms.Add(oto.Alias.ToLowerInvariant().Replace(" ", ""));
                        try {
                            oto.SearchTerms.Add(WanaKana.ToRomaji(oto.Alias).ToLowerInvariant().Replace(" ", ""));
                        } catch { }
                    });
            });
        }

        public override void Save() {
            try {
                otoWatcher.Paused = true;
                foreach (var oto in Otos) {
                    oto.WriteBack();
                }
                VoicebankLoader.WriteOtoSets(voicebank);
            } finally {
                otoWatcher.Paused = false;
            }
        }

        /// <summary>
        /// 释放声库常驻数据。此前**完全未覆写**（基类空实现），于是
        /// <c>SingerManager.ReleaseSingersNotInUse</c> 对经典歌手是空操作：
        /// loaded 永远为 true、oto 映射永不释放，换工程/删轨后内存只涨不落
        /// （上游 #2408 修的是"loaded 未复位"，Plus 连这一步都缺）。
        ///
        /// 这里只复位 loaded 并断开文件监视，**不清空 subbanks/otos/otoMap**：
        /// 清空会与并发渲染线程正在遍历这些集合的读取相撞（上游用后来的
        /// "原子 oto 快照"提交 83e02c7e 才安全地做到真释放）。复位 loaded 后，
        /// 下次 EnsureLoaded → Reload → Load 会原地重建全部集合，不依赖旧内容。
        /// </summary>
        public override void FreeMemory() {
            Log.Information($"Freeing memory for singer {Id}");
            loaded = false;
            // 断开 oto 目录监视（下次 Reload 会按需重建）；不置 null 是避免后续
            // Save() 的空引用。这里只停用监视器，不触碰正在被渲染线程读取的集合。
            otoWatcher?.Dispose();
        }

        public override bool TryGetOto(string phoneme, out UOto oto) {
            if (otoMap.TryGetValue(phoneme, out oto)) {
                return true;
            }
            return false;
        }

        public override bool TryGetMappedOto(string phoneme, int tone, out UOto oto) {
            oto = default;
            var subbank = subbanks.Find(subbank => string.IsNullOrEmpty(subbank.Color) && subbank.toneSet.Contains(tone));
            if (subbank != null && otoMap.TryGetValue($"{subbank.Prefix}{phoneme}{subbank.Suffix}", out oto)) {
                return true;
            }
            return TryGetOto(phoneme, out oto);
        }

        public override bool TryGetMappedOto(string phoneme, int tone, string color, out UOto oto) {
            oto = default;
            var subbank = subbanks.Find(subbank => subbank.Color == color && subbank.toneSet.Contains(tone));
            if (subbank != null && otoMap.TryGetValue($"{subbank.Prefix}{phoneme}{subbank.Suffix}", out oto)) {
                return true;
            }
            return TryGetMappedOto(phoneme, tone, out oto);
        }

        public override IEnumerable<UOto> GetSuggestions(string text) {
            if (text != null) {
                text = text.ToLowerInvariant().Replace(" ", "");
            }
            bool all = string.IsNullOrEmpty(text);
            return otoMap.Values
                .Where(oto => all || oto.SearchTerms.Exists(term => term.Contains(text)));
        }

        public override byte[] LoadPortrait() {
            return string.IsNullOrEmpty(Portrait)
                ? null
                : File.ReadAllBytes(Portrait);
        }
    }
}
