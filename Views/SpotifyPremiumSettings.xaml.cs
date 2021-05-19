using Flow.Launcher.Plugin.SpotifyPremium.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Flow.Launcher.Plugin.SpotifyPremium.Views
{
    /// <summary>
    /// Interaction logic for SpotifyPremiumSettings.xaml
    /// </summary>
    public partial class SpotifyPremiumSettings
    {
        private readonly SettingsViewModel viewModel;

        public SpotifyPremiumSettings(SettingsViewModel viewModel){
            InitializeComponent();
            this.viewModel = viewModel;
            RefreshView();
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            RefreshView();
        }

        public void RefreshView()
        {
            btnTest.Visibility = Visibility.Visible;
        }
    }
}