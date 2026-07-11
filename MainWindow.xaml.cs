using System;
using System.Windows;

namespace Voice
{
    public partial class MainWindow : Window
    {
        private readonly AudioEngine _audioEngine;

        public MainWindow()
        {
            InitializeComponent();
            
            // 1. Instantiate the shared AudioEngine
            _audioEngine = new AudioEngine();

            // 2. Pass AudioEngine references to each view page
            DashboardPanel.SetAudioEngine(_audioEngine);
            HistoryPanel.SetAudioEngine(_audioEngine);

            // 3. Connect cross-view request events
            DashboardPanel.AnalysisRequested += OnAnalysisRequested;
            HistoryPanel.RecordSelected += OnRecordSelected;
        }

        private void OnAnalysisRequested(VoiceAnalysisSession session, string wavPath)
        {
            // Load and display session parameters
            AnalysisPanel.DisplaySession(session);
            
            // Navigate to Analysis View
            NavAnalysis.IsChecked = true;
            
            // Refresh history tab in background
            HistoryPanel.LoadHistoryRecords();
        }

        private void OnRecordSelected(VoiceSessionRecord record)
        {
            // Load and display historical record parameters
            AnalysisPanel.DisplayRecord(record);
            
            // Navigate to Analysis View
            NavAnalysis.IsChecked = true;
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            // Ensure views are loaded
            if (DashboardPanel == null || AnalysisPanel == null || HistoryPanel == null)
            {
                return;
            }

            // Hide all views first
            DashboardPanel.Visibility = Visibility.Collapsed;
            AnalysisPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Collapsed;

            // Stop any active audio playbacks when navigating between tabs
            _audioEngine?.StopPlayback();

            // Show selected view
            if (NavCapture.IsChecked == true)
            {
                DashboardPanel.Visibility = Visibility.Visible;
            }
            else if (NavAnalysis.IsChecked == true)
            {
                AnalysisPanel.Visibility = Visibility.Visible;
            }

            else if (NavHistory.IsChecked == true)
            {
                HistoryPanel.Visibility = Visibility.Visible;
                HistoryPanel.LoadHistoryRecords(); // refresh history log
            }
        }

        // ================= CUSTOM TITLE BAR COMMANDS =================

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (MainBorder == null) return;

            if (this.WindowState == WindowState.Maximized)
            {
                // Remove outer 1px border when maximized to align flush with display edges
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                // Show 1px border when in windowed mode
                MainBorder.BorderThickness = new Thickness(1);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Thread-safe disposal of AudioEngine to free the audio input device
            _audioEngine.Dispose();
            base.OnClosed(e);
        }
    }
}