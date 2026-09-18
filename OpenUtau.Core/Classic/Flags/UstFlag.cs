
using System;

namespace OpenUtau.Classic.Flags {
    public class UstFlag {
        public readonly string Key;
        public readonly int Value;

        public UstFlag(string key, int value) {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value;
        }

        /// <summary>还原为 UST 标记文本（Key+Value）——导入失败时的错误信息需要它，
        /// 否则只会打印类名（上游 a46de4e0）。</summary>
        public override string ToString() {
            return Key + Value;
        }
    }
}
