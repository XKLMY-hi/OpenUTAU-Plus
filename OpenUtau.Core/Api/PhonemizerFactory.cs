using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace OpenUtau.Api {
    public class PhonemizerFactory {
        public Type type;
        public string name;
        public string tag;
        public string author;
        public string language;

        public Phonemizer Create() {
            var phonemizer = Activator.CreateInstance(type) as Phonemizer;
            phonemizer.Name = name;
            phonemizer.Tag = tag;
            phonemizer.Language = language;
            return phonemizer;
        }

        public override string ToString() => string.IsNullOrEmpty(author)
            ? $"[{tag}] {name}"
            : $"[{tag}] {name} (Contributed by {author})";

        // 工厂缓存必须线程安全（上游 7684d706 同款修复）：BuildList/Get 会在插件扫描线程、
        // 渲染线程与 UI 线程并发调用——普通 Dictionary 并发写会损坏内部结构（无限循环/
        // 抛异常），并发读-写枚举也会抛 InvalidOperationException。
        private static readonly ConcurrentDictionary<Type, PhonemizerFactory> factories = new();
        private static PhonemizerFactory[] orderedFactories = [];
        public static PhonemizerFactory Get(Type type) {
            if (factories.TryGetValue(type, out var factory)) {
                return factory;
            }
            var attr = type.GetCustomAttribute<PhonemizerAttribute>();
            if (attr == null || string.IsNullOrEmpty(attr.Name) || string.IsNullOrEmpty(attr.Tag)) {
                return null;
            }
            factory = new PhonemizerFactory() {
                type = type,
                name = attr.Name,
                tag = attr.Tag,
                author = attr.Author,
                language = attr.Language,
            };
            return factories.GetOrAdd(type, factory);
        }

        public static PhonemizerFactory? Get(string typeFullName) {
            foreach (var factory in factories.Values) {
                if (factory.type.FullName == typeFullName) {
                    return factory;
                }
            }
            return null;
        }

        public static void BuildList() {
            orderedFactories = factories.Values.OrderBy(f => f.tag).ToArray();
        }

        public static PhonemizerFactory[] GetAll() => orderedFactories;
    }
}
