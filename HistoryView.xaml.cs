using System;
using System.Windows;
using System.Windows.Controls;

namespace Voice
{
    public partial class HistoryView : UserControl
    {
        private AudioEngine? _audioEngine;
        private Button? _activePlayButton;

        // Events to interact with MainWindow
        public event Action<VoiceSessionRecord>? RecordSelected;

        public HistoryView()
        {
            InitializeComponent();
            this.Loaded += HistoryView_Loaded;
        }

        public void SetAudioEngine(AudioEngine audioEngine)
        {
            _audioEngine = audioEngine;
        }

        private void HistoryView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistoryRecords();
        }

        public void LoadHistoryRecords()
        {
            var records = SessionHistory.LoadHistory();
            
            if (records == null || records.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                HistoryListView.Visibility = Visibility.Collapsed;
                HistoryListView.ItemsSource = null;
            }
            else
            {
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                HistoryListView.Visibility = Visibility.Visible;
                HistoryListView.ItemsSource = records;
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadHistoryRecords();
        }

        private void PlayRecord_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            if (_activePlayButton == button)
            {
                _audioEngine?.StopPlayback();
                return;
            }

            if (_audioEngine == null) return;

            if (_activePlayButton != null)
            {
                _audioEngine.StopPlayback();
            }

            if (button.DataContext is VoiceSessionRecord record)
            {
                if (string.IsNullOrEmpty(record.AudioPath) || !System.IO.File.Exists(record.AudioPath))
                {
                    MessageBox.Show("The recording audio file could not be found.", "File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _activePlayButton = button;
                SetButtonText(_activePlayButton, "Stop");
                SetPlayButtonIcon(_activePlayButton, "\uE71A");

                var playingButton = _activePlayButton;
                _audioEngine.PlaybackRecording(record.AudioPath, null, () =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_activePlayButton == playingButton)
                        {
                            SetButtonText(playingButton, "Play");
                            SetPlayButtonIcon(playingButton, "\uE768");
                            _activePlayButton = null;
                        }
                    }));
                });
            }
        }

        private void SetPlayButtonIcon(Button button, string glyph)
        {
            if (button.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.FontFamily.Source == "Segoe MDL2 Assets")
                    {
                        tb.Text = glyph;
                        break;
                    }
                }
            }
        }

        private void SetButtonText(Button button, string text)
        {
            if (button.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.FontFamily.Source != "Segoe MDL2 Assets")
                    {
                        tb.Text = text;
                        break;
                    }
                }
            }
        }

        private void LoadRecord_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (button.DataContext is VoiceSessionRecord record)
            {
                RecordSelected?.Invoke(record);
            }
        }

        private void DeleteRecord_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (button.DataContext is VoiceSessionRecord record)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to permanently delete this voice session record and its recording file?", 
                    "Confirm Delete", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    SessionHistory.DeleteRecord(record);
                    LoadHistoryRecords();
                }
            }
        }
    }
}
