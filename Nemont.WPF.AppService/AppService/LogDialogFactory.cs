﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Nemont.WPF.AppService.Threading;

namespace Nemont.WPF.AppService
{
    internal delegate void RaiseProgressChangedHandler(string text, double value);

    public class LogDialogFactory : LogFactory
    {
        public override event RaiseLogChangedHandler OnLogChanged;
        internal event RaiseProgressChangedHandler OnProgressChanged;

        private DMessageDialog DMessageDialog;
        private MessageDialog WMessageDialog;
        private bool IsShowDialog = false;
        private bool IsProcessMode = false;
        public bool AutoShow = true;

        private string progressText;
        private string ProgressText {
            get { return progressText; }
            set {
                progressText = value;

                if (Stopwatch.ElapsedMilliseconds > UpdateIntervalTime) {
                    if (IsShowDialog == false && AutoShow == true) {
                        Application.Current.Dispatcher.Invoke(() => WMessageDialog.Show());                        
                    }
                    OnProgressChanged?.Invoke(value, ProgressValue);

                    if (IsProcessMode == false) {
                        Application.Current.Dispatcher.Invoke((ThreadStart)(() => { }), DispatcherPriority.ApplicationIdle);
                    }

                    Stopwatch.Restart();
                }
            }
        }

        private double progressValue;
        private double ProgressValue {
            get { return progressValue; }
            set {
                progressValue = value;

                if (Stopwatch.ElapsedMilliseconds > UpdateIntervalTime) {
                    if (IsShowDialog == false && AutoShow == true) {
                        Application.Current.Dispatcher.Invoke(() => WMessageDialog.Show());
                    }

                    OnProgressChanged?.Invoke(ProgressText, value);

                    if (IsProcessMode == false) {
                        Application.Current.Dispatcher.Invoke((ThreadStart)(() => { }), DispatcherPriority.ApplicationIdle);
                    }

                    Stopwatch.Restart();
                }
            }
        }

        public override string Log {
            get { return log; }
            set {
                log = value;

                if (Stopwatch.ElapsedMilliseconds > UpdateIntervalTime) {
                    if (IsShowDialog == false && AutoShow == true) {
                        Application.Current.Dispatcher.Invoke(() => WMessageDialog.Show());
                    }

                    OnLogChanged?.Invoke(value);

                    if (IsProcessMode == false) {
                        Application.Current.Dispatcher.Invoke((ThreadStart)(() => { }), DispatcherPriority.ApplicationIdle);
                    }

                    Stopwatch.Restart();
                }
            }
        }

        public LogDialogFactory() : base()
        {
            DMessageDialog = new DMessageDialog();
            WMessageDialog = new MessageDialog(DMessageDialog);

            OnLogChanged += DMessageDialog.OnMessageChanged;
            OnProgressChanged += DMessageDialog.OnProgressChanged;

            DMessageDialog.RcClear = new Service.RelayCommand(() => { Clear(); });
        }

        public LogDialogFactory(StartInfo dialogStartInfo) : this()
        {
            SetDialogInfo(dialogStartInfo);
        }

        public void RunTask(Action<LogDialogFactory> method)
        {
            Action action = () => {
                try {
                    Task.Process.Kill();
                }
                catch {
                    Debug.WriteLine("Failed to kill process.");
                }
                method.Invoke(this);
            };
            _RunTask(action);
        }

        public override void RunTask(Action method)
        {
            Action action = () => {
                try {
                    Task.Process.Kill();
                }
                catch {
                    Debug.WriteLine("Failed to kill process.");
                }
                method.Invoke();
            };
            _RunTask(action);
        }

        protected override void _RunTask(Action action)
        {
            if (Task != null && Task.IsBusy == true) {
                return;
            }

            IsProcessMode = DMessageDialog.IsProcessMode = true;
            WMessageDialog.ButtonCancel.Click += (sender, e) => {
                var res = MessageBox.Show("Do you want to stop task?", "Message", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.No) {
                    return;
                }
                Task.OnStopProcess();
            };

            base._RunTask(action);

            if (IsShowDialog == true) {
                WMessageDialog.ShowDialog();
            }
        }

        protected override void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try {
                Task.WorkerCompleteAction?.Invoke();

                if (Task.Exception == null) {
                    Write(TaskMessageCompleted);
                    Flush();

                    if (Task.IsWarning == false) {
                        WMessageDialog.Close();
                    }
                }
                else {
                    if (Task.Exception is TaskCanceledException) {
                        Write(TaskMessageCanceled);
                        Flush();

                        WMessageDialog.Close();

                        return;
                    }
                    Write(TaskMessageErrorOccurred);
                    WriteLine(Task.Exception.Message);
                    WriteLine(Task.Exception.StackTrace);
                    Flush();
                }
            }
            finally {
                if (IsClearTaskMessageTerminated == true) {
                    TaskMessageCompleted = DefaultTaskMessageCompleted;
                    TaskMessageCanceled = DefaultTaskMessageCanceled;
                    TaskMessageErrorOccurred = DefaultTaskMessageErrorOccurred;
                }
                Task.IsCompleted = true;
                IsProcessMode = DMessageDialog.IsProcessMode = false;
                CompleteProgress();
                Task = null;
            }
        }

        /// <summary>
        /// StartInfo로부터 LogDialog의 속성을 설정합니다.
        /// </summary>
        /// <param name="dialogStartInfo">설정할 속성이 있는 StartInfo입니다.</param>
        public void SetDialogInfo(StartInfo dialogStartInfo)
        {
            WMessageDialog.Owner = dialogStartInfo.Owner ?? WMessageDialog.Owner;
            WMessageDialog.Width = dialogStartInfo.Width ?? WMessageDialog.Width;
            WMessageDialog.Height = dialogStartInfo.Height ?? WMessageDialog.Height;
            WMessageDialog.Title = dialogStartInfo.Title ?? WMessageDialog.Title;
            WMessageDialog.ShowInTaskbar = dialogStartInfo.ShowInTaskBar ?? WMessageDialog.ShowInTaskbar;
            IsShowDialog = dialogStartInfo.IsDialog;
        }

        /// <summary>
        /// 진행 상황 텍스트를 설정합니다.
        /// </summary>
        /// <param name="text">설정할 진행 상황 텍스트입니다.</param>
        public void SetProgressText(string text)
        {
            ProgressText = text;
        }

        /// <summary>
        /// 진행 상황 막대값을 설정합니다.
        /// </summary>
        /// <param name="value">설정할 막대값입니다. 범위는 0에서 1 사이입니다.</param>
        public void SetProgressValue(double value)
        {
            ProgressValue = value;
        }

        /// <summary>
        /// 진행 상황 컨텍스트를 초기화하고 다이얼로그에 표시합니다.
        /// </summary>
        public void InitializeProgress()
        {
            WMessageDialog.Dispatcher.Invoke(() => {
                DMessageDialog.ProgressVisibility = Visibility.Visible;
                DMessageDialog.ProgressText = "";
                DMessageDialog.ProgressValue = 0.0;
            });
        }

        /// <summary>
        /// 진행 상황 컨텍스를 다이얼로그에서 숨깁니다.
        /// </summary>
        public void CompleteProgress()
        {
            WMessageDialog.Dispatcher.Invoke(() => {
                DMessageDialog.ProgressVisibility = Visibility.Collapsed;
            });
        }

        public void ShowDialog()
        {
            if (IsShowDialog == true) {
                WMessageDialog.ShowDialog();
            }
            else {
                WMessageDialog.Show();
            }
        }

        /// <summary>
        /// 메시지 다이얼로그를 숨깁니다.
        /// </summary>
        public void HideDialog()
        {
            WMessageDialog.Close();
        }

        /// <summary>
        /// 메시지 다이얼로그를 강제로 닫습니다. 이 메서드를 호출하면 인스턴스를 더 이상 사용할 수 없습니다.
        /// </summary>
        public void CloseDialog()
        {
            WMessageDialog.ForceClose();
        }

        /// <summary>
        /// 작업 중단 또는 종료 시 표시되지 않은 로그를 모두 출력하도록 인보크를 수행합니다.
        /// </summary>
        public override void Flush()
        {
            OnLogChanged?.Invoke(log);
            OnProgressChanged?.Invoke(progressText, progressValue);
        }
    }
}
