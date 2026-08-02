using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpenUtau.Core;
using Serilog;
using SukiUI.Controls;
using SukiUI.MessageBox;

namespace OpenUtau.App.Views {
    /// <summary>
    /// MessageBox 门面（阶段 D）：自研窗口 → SukiMessageBox 渲染（SukiWindow 玻璃卡片 + 主题跟随）。
    /// 60+ 调用点签名零改动：Show/ShowError/ShowModal/ShowProcessing + 结果枚举。
    /// - Show：简单确认框走 SukiMessageBox.ShowDialogResult 预设按钮
    /// - ShowError：host 自定义内容（链接化文本 + 详情 Expander + 复制按钮）+ Error 图标
    /// - ShowModal/ShowProcessing：host 自定义进度内容；窗口引用经
    ///   AttachedToVisualTree → GetTopLevel 获取（SukiMessageBox 不暴露窗口实例）
    /// </summary>
    public class MessageBox {
        public enum MessageBoxButtons { Ok, OkCancel, YesNo, YesNoCancel, OkCopy }
        public enum MessageBoxResult { Ok, Cancel, Yes, No }

        // ShowModal/ShowProcessing 持有的运行时状态
        private SukiMessageBoxHost? _host;
        private TextBlock? _contentText;
        private Window? _window;
        private bool _closeRequested;

        /// <summary>窗口关闭时触发（ShowModal 调用方用它感知取消）。</summary>
        public event EventHandler? Closed;

        /// <summary>更新对话框正文（进度文本等）。</summary>
        public void SetText(string text) {
            Dispatcher.UIThread.Post(() => {
                if (_contentText != null) {
                    _contentText.Text = text;
                }
            });
        }

        /// <summary>程序化关闭对话框（幂等；窗口未挂载时延迟到挂载后执行）。</summary>
        public void Close() {
            Dispatcher.UIThread.Post(() => {
                if (_window == null) {
                    _closeRequested = true;
                    return;
                }
                _window.Close();
            });
        }

        /// <summary>
        /// 内容控件挂载后捕获宿主窗口（SukiMessageBox 创建的内部 SukiWindow 不对外暴露）。
        /// 窗口关闭时引用置空，Close() 自然幂等。
        /// </summary>
        private void CaptureWindow(Control content) {
            content.AttachedToVisualTree += (_, __) => {
                _window = TopLevel.GetTopLevel(content) as Window;
                if (_window != null) {
                    _window.Closed += (_, _) => _window = null;
                }
                if (_closeRequested && _window != null) {
                    _closeRequested = false;
                    _window.Close();
                }
            };
        }

        /// <summary>错误对话框：异常翻译/聚合/版本号逻辑保留，渲染走 SukiMessageBox host。</summary>
        public static Task<MessageBoxResult> ShowError(Window parent, Exception? e, string message = "", bool fromNotif = false) {
            string text = message;
            string title = ThemeManager.GetString("errors.caption");
            if (fromNotif) {
                IReadOnlyList<Window> dialogs = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).Windows;
                foreach (var dialog in dialogs) {
                    if (dialog.IsActive) {
                        parent = dialog;
                        break;
                    }
                }
            }

            var builder = new StringBuilder();
            if (e != null) {
                if (e is AggregateException ae && ae.Flatten().InnerExceptions.Count == 1) {
                    e = ae.InnerExceptions.First();
                }

                if (e is MessageCustomizableException mce) {
                    text = Translate(mce);
                    builder.AppendLine(mce.SubstanceException.Message);
                    builder.AppendLine();
                    builder.Append(mce.SubstanceException.ToString());
                    if (!mce.ShowStackTrace) {
                        return Show(parent, text, title, MessageBoxButtons.Ok);
                    }
                } else if (e is AggregateException nestedAe) {
                    foreach (var ie in nestedAe.Flatten().InnerExceptions) {
                        if (!string.IsNullOrWhiteSpace(text)) {
                            text += "\n";
                        }
                        if (ie is MessageCustomizableException innnerMce) {
                            text += Translate(innnerMce);
                            builder.AppendLine(innnerMce.SubstanceException.Message);
                            builder.AppendLine();
                            builder.Append(innnerMce.SubstanceException.ToString());
                        } else {
                            text += ie.Message;
                            builder.AppendLine(ie.Message);
                            builder.AppendLine();
                            builder.AppendLine(ie.ToString());
                        }
                        builder.AppendLine();
                    }
                } else {
                    builder.AppendLine(e.Message);
                    builder.AppendLine();
                    builder.Append(e.ToString());
                    if (string.IsNullOrEmpty(text)) {
                        text = e.Message;
                    }
                }
            }
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version");

            return Show(parent, text, title, MessageBoxButtons.OkCopy, builder.ToString());

            string Translate(MessageCustomizableException mce) {
                string text;
                if (string.IsNullOrWhiteSpace(mce.TranslatableMessage)) {
                    text = mce.Message;
                } else {
                    text = mce.TranslatableMessage;
                    try {
                        var matches = Regex.Matches(mce.TranslatableMessage, "<translate:(.*?)>");
                        foreach (Match match in matches) {
                            if (ThemeManager.TryGetString(match.Groups[1].Value, out string translated)) {
                                text = text.Replace(match.Value, translated);
                            } else {
                                text = mce.Message;
                                break;
                            }
                        }
                    } catch {
                        text = mce.Message;
                    }
                }

                if (mce.Replaces != null && mce.Replaces.Length > 0) {
                    return string.Format(text, mce.Replaces);
                } else {
                    return text;
                }
            }
        }

        /// <summary>
        /// 普通对话框：Ok/OkCancel/YesNo/YesNoCancel 走 SukiMessageBox 预设按钮；OkCopy 走 host（复制按钮）。
        /// 注意：门面保持同步方法返回 Task（旧实现即如此），避免 CS4014 波及相关 async 调用点。
        /// </summary>
        public static Task<MessageBoxResult> Show(Window parent, string text, string title, MessageBoxButtons buttons, string? stackTrace = null) {
            if (buttons == MessageBoxButtons.OkCopy) {
                return ShowErrorHost(parent, text, title, stackTrace);
            }
            var sukiButtons = buttons switch {
                MessageBoxButtons.Ok => SukiMessageBoxButtons.OK,
                MessageBoxButtons.OkCancel => SukiMessageBoxButtons.OKCancel,
                MessageBoxButtons.YesNo => SukiMessageBoxButtons.YesNo,
                _ => SukiMessageBoxButtons.YesNoCancel,
            };
            return ShowCore(parent, text, title, buttons, sukiButtons);
        }

        private static async Task<MessageBoxResult> ShowCore(Window parent, string text, string title, MessageBoxButtons buttons, SukiMessageBoxButtons sukiButtons) {
            var result = await SukiMessageBox.ShowDialogResult(parent, text, sukiButtons, title);
            return MapResult(result, buttons);
        }

        /// <summary>错误详情 host：链接化正文 + 详情 Expander + [OK][复制] 按钮 + Error 图标。</summary>
        private static async Task<MessageBoxResult> ShowErrorHost(Window parent, string text, string title, string? stackTrace) {
            var contentPanel = new StackPanel {
                MaxWidth = 560,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
            };
            SetTextWithLink(text, contentPanel);
            if (stackTrace != null) {
                var stackTracePanel = new StackPanel();
                contentPanel.Children.Add(new Expander {
                    Header = ThemeManager.GetString("errors.details"),
                    Content = stackTracePanel,
                });
                SetTextWithLink(stackTrace, stackTracePanel);
            }

            var host = new SukiMessageBoxHost {
                Header = title,
                IconPreset = SukiMessageBoxIcons.Error,
                Content = contentPanel,
            };
            var okBtn = SukiMessageBoxButtonsFactory.CreateButton(
                ThemeManager.GetString("button.ok"), SukiMessageBoxResult.OK, "Flat");
            var copyBtn = SukiMessageBoxButtonsFactory.CreateButton(
                ThemeManager.GetString("dialogs.messagebox.copy"), null, "Flat");
            copyBtn.Click += (_, __) => {
                try {
                    var data = new Avalonia.Input.DataTransfer();
                    data.Add(Avalonia.Input.DataTransferItem.CreateText(text + "\n" + stackTrace));
                    _ = TopLevel.GetTopLevel(parent)?.Clipboard?.SetDataAsync(data);
                } catch { }
            };
            host.ActionButtonsSource = new AvaloniaList<Button> { okBtn, copyBtn };

            var result = await SukiMessageBox.ShowDialog(parent, host, title);
            if (result is SukiMessageBoxResult r) {
                return MapResult(r, MessageBoxButtons.OkCopy);
            }
            return DefaultResult(MessageBoxButtons.OkCopy); // 关闭窗口（ESC/X）→ 默认按钮
        }

        /// <summary>模态进度框：host 进度内容 + OK 按钮；返回实例供 SetText/Close/Closed 使用。</summary>
        public static MessageBox ShowModal(Window parent, string text, string title) {
            var msgbox = new MessageBox();
            var content = new TextBlock {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 560,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var host = new SukiMessageBoxHost { Header = title, Content = content };
            host.ActionButtonsSource = new AvaloniaList<Button> {
                SukiMessageBoxButtonsFactory.CreateButton(
                    ThemeManager.GetString("button.ok"), SukiMessageBoxResult.OK, "Flat"),
            };
            msgbox._host = host;
            msgbox._contentText = content;
            msgbox.CaptureWindow(content);
            _ = SukiMessageBox.ShowDialog(parent, host, title).ContinueWith(t => {
                msgbox.Closed?.Invoke(msgbox, EventArgs.Empty);
            }, TaskScheduler.FromCurrentSynchronizationContext());
            return msgbox;
        }

        /// <summary>
        /// 后台任务进度框：host 进度内容 + 取消按钮。取消 → token 取消；任务完成 → 自动关窗。
        /// 语义与旧实现一致：返回执行任务本身（fault 检查、取消时 Result=Cancel）。
        /// </summary>
        public static Task<MessageBoxResult> ShowProcessing(
                Window parent,
                string text,
                string title,
                Action<MessageBox, CancellationToken> action,
                Action<Task>? onFinished = null) {
            var msgbox = new MessageBox();
            var content = new TextBlock {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 560,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var host = new SukiMessageBoxHost { Header = title, Content = content };
            host.ActionButtonsSource = new AvaloniaList<Button> {
                SukiMessageBoxButtonsFactory.CreateButton(
                    ThemeManager.GetString("button.cancel"), SukiMessageBoxResult.Cancel, "Flat"),
            };
            msgbox._host = host;
            msgbox._contentText = content;
            msgbox.CaptureWindow(content);

            var res = MessageBoxResult.Ok;
            var tokenSource = new CancellationTokenSource();
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var task = Task.Run(() => {
                action.Invoke(msgbox, tokenSource.Token);
                return res;
            }, tokenSource.Token);

            // 任务完成 → 关窗（若还在）+ 通知调用方
            task.ContinueWith(t => {
                msgbox.Close();
                if (onFinished != null) {
                    onFinished(task);
                }
            }, scheduler);

            // 窗口关闭（用户点取消/关窗）→ 若任务未完成则取消
            _ = SukiMessageBox.ShowDialog(parent, host, title).ContinueWith(dialogTask => {
                if (!task.IsCompleted) {
                    res = MessageBoxResult.Cancel;
                    tokenSource.Cancel();
                }
            });

            return task;
        }

        private static MessageBoxResult MapResult(SukiMessageBoxResult r, MessageBoxButtons buttons) {
            return r switch {
                SukiMessageBoxResult.OK => MessageBoxResult.Ok,
                SukiMessageBoxResult.Cancel => MessageBoxResult.Cancel,
                SukiMessageBoxResult.Yes => MessageBoxResult.Yes,
                SukiMessageBoxResult.No => MessageBoxResult.No,
                _ => DefaultResult(buttons), // Close（ESC/X）
            };
        }

        /// <summary>旧实现"关闭窗口返回最后设置的默认按钮"语义。</summary>
        private static MessageBoxResult DefaultResult(MessageBoxButtons buttons) {
            return buttons switch {
                MessageBoxButtons.Ok => MessageBoxResult.Ok,
                MessageBoxButtons.OkCancel => MessageBoxResult.Cancel,
                MessageBoxButtons.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.Cancel, // YesNoCancel
            };
        }

        private static void SetTextWithLink(string text, StackPanel textPanel) {
            // @"http(s)?://([\w-]+\.)+[\w-]+(/[A-Z0-9-.,_/?%&=]*)?"
            var regex = new Regex(@"http(s)?://[^(\r\n|\n| )]+", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = regex.Match(text);
            if (match.Success) {
                textPanel.Children.Add(new TextBlock { Text = text.Substring(0, match.Index) });
                var hyperlink = new Button {
                    Content = match.Value.Trim(),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Classes = { "linkButton" },
                };
                hyperlink.Click += OnUrlClick;
                textPanel.Children.Add(hyperlink);

                SetTextWithLink(text.Substring(match.Index + match.Length), textPanel);
            } else {
                if (!string.IsNullOrEmpty(text)) {
                    textPanel.Children.Add(new TextBlock { Text = text });
                }
            }
        }

        private static void OnUrlClick(object? sender, RoutedEventArgs e) {
            try {
                if (sender is Button button && button.Content is string url) {
                    OS.OpenWeb(url);
                }
            } catch (Exception ex) {
                Log.Error(ex, "Failed to open url");
            }
        }
    }
}
