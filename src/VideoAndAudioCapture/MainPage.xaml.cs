using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.Render;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace VideoAndAudioCapture
{
    public sealed partial class MainPage : Page
    {
        private DeviceInformation _videoInputSelected;
        private DeviceInformation _audioInputSelected;

        private MediaCapture _mediaCapture;
        private AudioGraph _audioGraph;

        private AudioDeviceInputNode _deviceInputNode;
        private AudioDeviceOutputNode _deviceOutputNode;
        
        private DisplayRequest _displayRequest;

        private bool _displayRequested;
        private bool _isPreviewing;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadDevices();
        }

        private async Task LoadDevices()
        {
            VideoInputComboBox.ItemsSource = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            AudioInputComboBox.ItemsSource = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector());
        }

        private void VideoInputComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _videoInputSelected = e.AddedItems?.FirstOrDefault() as DeviceInformation;
            CheckStartButton();
        }

        private void AudioInputComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _audioInputSelected = e.AddedItems?.FirstOrDefault() as DeviceInformation;
            CheckStartButton();
        }

        private async void StartButtonClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await StopVideoAsync();
            StopAudio();

            await StartVideoAsync();
            await StartAudioAsync();

            StopButton.IsEnabled = true;
        }

        private async void StopButtonClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            StopButton.IsEnabled = true;

            await StopVideoAsync();
            StopAudio();
        }

        private async Task StartVideoAsync()
        {
            try
            {
                _mediaCapture = new MediaCapture();

                if (_mediaCapture != null)
                {
                    await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings() { VideoDeviceId = _videoInputSelected.Id });
                    CaptureElement.Source = _mediaCapture;
                    await _mediaCapture.StartPreviewAsync();

                    _isPreviewing = true;
                    _displayRequest = new DisplayRequest();
                    _displayRequest.RequestActive();
                    _displayRequested = true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                Debug.WriteLine("The app was denied access to the camera");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MediaCapture initialization failed. {ex?.Message}");
            }
        }

        private async Task StartAudioAsync()
        {
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

            try
            {
                CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
                if (result.Status != AudioGraphCreationStatus.Success) return;

                _audioGraph = result.Graph;

                // Create a device input node
                CreateAudioDeviceInputNodeResult deviceInputNodeResult = await _audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Media, _audioGraph.EncodingProperties, _audioInputSelected);
                if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success) return;

                // Create a device output node
                CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await _audioGraph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success) return;

                _deviceInputNode = deviceInputNodeResult.DeviceInputNode;
                _deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

                _deviceInputNode.AddOutgoingConnection(_deviceOutputNode);
                _audioGraph.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioGraph initialization failed. {ex?.Message}");
            }
        }

        private async Task StopVideoAsync()
        {
            if (_mediaCapture != null && _isPreviewing)
            {
                try
                {
                    _isPreviewing = false;
                    await _mediaCapture.StopPreviewAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception when stopping the preview: {0}", ex.ToString());
                }

                // Use the dispatcher because this method is sometimes called from non-UI threads
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Cleanup the UI
                    CaptureElement.Source = null;

                    // Allow the device screen to sleep now that the preview is stopped
                    if (_displayRequest != null && _displayRequested)
                    {
                        _displayRequest.RequestRelease();
                    }
                });

                _mediaCapture.Dispose();
                _mediaCapture = null;
            }
        }

        private void StopAudio()
        {
            if (_audioGraph != null)
            {
                try
                {
                    _audioGraph.Stop();

                    _deviceInputNode?.Dispose();
                    _deviceOutputNode?.Dispose();
                    _audioGraph.Dispose();

                    _audioGraph = null;
                    _deviceInputNode = null;
                    _deviceOutputNode = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception when stopping the AudioGraph: {0}", ex.ToString());
                }
            }
        }

        private void CheckStartButton()
        {
            StartButton.IsEnabled = _videoInputSelected != null && _audioInputSelected != null;
        }
    }
}
