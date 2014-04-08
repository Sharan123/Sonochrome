using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Color2Name;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using Windows.Media.Capture;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Sonochrome
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        C2n temp;
        HelperColor hc;
        WriteableBitmap slika;
        private Windows.Media.Capture.MediaCapture m_mediaCaptureMgr;
        private bool m_bRecording;
        private bool m_bSuspended;
        private bool m_bPreviewing;
        bool canpreview, canrecord, cantakephoto;
        public static MainPage Current;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private String Color2Hex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        private void SetupVideoDeviceControl(Windows.Media.Devices.MediaDeviceControl videoDeviceControl, Slider slider)
        {
            try
            {
                if ((videoDeviceControl.Capabilities).Supported)
                {
                    slider.IsEnabled = true;
                    slider.Maximum = videoDeviceControl.Capabilities.Max;
                    slider.Minimum = videoDeviceControl.Capabilities.Min;
                    slider.StepFrequency = videoDeviceControl.Capabilities.Step;
                    double controlValue = 0;
                    if (videoDeviceControl.TryGetValue(out controlValue))
                    {
                        slider.Value = controlValue;
                    }
                }
                else
                {
                    slider.IsEnabled = false;
                }
            }
            catch (Exception e)
            {
                //ShowExceptionMessage(e);
            }
        }

        internal async void button1_Click(object sender, RoutedEventArgs e)
        {
            /* Way one to use camera -> Full screen mode*/
            /* CameraCapture(); */
            /* Way two CaptureElement and canvas */

            try
            {
                button1.IsEnabled = false;
                //ShowStatusMessage("Starting device");
                m_mediaCaptureMgr = new Windows.Media.Capture.MediaCapture();
                await m_mediaCaptureMgr.InitializeAsync();

                if (m_mediaCaptureMgr.MediaCaptureSettings.VideoDeviceId != "" && m_mediaCaptureMgr.MediaCaptureSettings.AudioDeviceId != "")
                {

                    canpreview = true;
                    canrecord = true;
                    cantakephoto = true;

                    //ShowStatusMessage("Device initialized successful");

                    m_mediaCaptureMgr.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(RecordLimitationExceeded);
                    m_mediaCaptureMgr.Failed += new Windows.Media.Capture.MediaCaptureFailedEventHandler(Failed);
                }
                else
                {
                    button1.IsEnabled = true;
                    //ShowStatusMessage("No VideoDevice/AudioDevice Found");
                }
            }
            catch (Exception exception)
            {
                //ShowExceptionMessage(exception);
            }

            /* So far the code was for starting the device*/

            /* Now the code to preview the image */

            m_bPreviewing = false;
            try
            {
                //ShowStatusMessage("Starting preview");
                button1.IsEnabled = false;
                canpreview = false;

                previewCanvas1.Visibility = Windows.UI.Xaml.Visibility.Visible;
                previewElement1.Source = m_mediaCaptureMgr;
                await m_mediaCaptureMgr.StartPreviewAsync();
                if ((m_mediaCaptureMgr.VideoDeviceController.Brightness != null) && m_mediaCaptureMgr.VideoDeviceController.Brightness.Capabilities.Supported)
                {
                    SetupVideoDeviceControl(m_mediaCaptureMgr.VideoDeviceController.Brightness, sldBrightness);
                }
                if ((m_mediaCaptureMgr.VideoDeviceController.Contrast != null) && m_mediaCaptureMgr.VideoDeviceController.Contrast.Capabilities.Supported)
                {
                    SetupVideoDeviceControl(m_mediaCaptureMgr.VideoDeviceController.Contrast, sldContrast);
                }
                rect1.Visibility = Visibility.Visible;
                m_bPreviewing = true;
                //ShowStatusMessage("Start preview successful");

                captureclr.IsEnabled = true;

            }
            catch (Exception exception)
            {
                m_bPreviewing = false;
                previewElement1.Source = null;
                /* If attempt to get preview fails he can try again */
                button1.IsEnabled = true;
                canpreview = true;
                //ShowExceptionMessage(exception);
            }
        }

            internal async void captureclr_Click(object sender, RoutedEventArgs e)
        {
            // FOR Performance
            // m_mediaCaptureMgr.PrepareLowLagPhotoCaptureAsync



            //m_mediaCaptureMgr.CapturePhotoToStreamAsync

            IRandomAccessStream i_rs = new InMemoryRandomAccessStream();
            
            ImageEncodingProperties ie_prop = new ImageEncodingProperties();
            ie_prop.Width = (uint)previewElement1.Width;
            ie_prop.Height = (uint)previewElement1.Height;
            ie_prop.Subtype = "JPEG";

            
            await m_mediaCaptureMgr.CapturePhotoToStreamAsync(ie_prop,i_rs);

            

            /* Losing the aspect ratio because we set both decode pixel width and heigth  */
            // if u want bitmap image
            /*
            BitmapImage bi = new BitmapImage();
            bi.DecodePixelHeight = (int) previewElement1.Height;
            bi.DecodePixelWidth = (int) previewElement1.Width;
            bi.SetSource(i_rs);
            */
            /* Now to change the bitmapimage to writeablebitmap image*/
            
            WriteableBitmap wb = new WriteableBitmap((int)previewElement1.Width, (int)previewElement1.Height);
            
            //wb.SetSource(i_rs);
            wb.SetSource(i_rs);
            
            /* Now we need to pull out the pixels (using writeablebitmapEX class) 
             pulling out every 9th pixel but none on the edges so thats 9x9=81 pixels for a 90x90 square
             */
            Color[,] rgb_array=new Color[9,9];
            // 180 is both left and top from both canvas and capture element

            Dictionary<String, int> mapa = new Dictionary<string, int>();
            Dictionary<HelperColor, int> mapa_hc=new Dictionary<HelperColor,int>();
            // TODO fix rgb_array null exception
            for (int i = 9; i  < 90; i=i+9)
                for (int j = 9; j < 90; j = j + 9)
                {
                    int l1 = (i - 9) / 9;
                    int l2 = (j - 9) / 9;
                    Color tc = wb.GetPixel(180+i,180+j);
                    rgb_array[l1, l2] = new Color();
                }
            for (int i = 0; i < 9; i = i + 1)
                for (int j = 9; j < 9; j = j + 1)
                {
                    HelperColor hc=temp.clr_name(Color2Hex(rgb_array[i,j]));
                    mapa_hc[hc]++;
                    mapa[hc.name_shade]++;
                }

           
            
        
        }

            public async void Failed(Windows.Media.Capture.MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
            {
                try
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        //ShowStatusMessage("Fatal error" + currentFailure.Message);
                    });
                }
                catch (Exception e)
                {
                    //ShowExceptionMessage(e);
                }
            }

            public async void RecordLimitationExceeded(Windows.Media.Capture.MediaCapture currentCaptureObject)
            {

                if (m_bRecording)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            //ShowStatusMessage("Stopping Record on exceeding max record duration");
                            await m_mediaCaptureMgr.StopRecordAsync();
                            m_bRecording = false;
                            // still no button for recording (aka. real time processing) hence the bool holders can record etc. 
                            //btnStartStopRecord1.Content = "StartRecord";
                            canrecord = true;
                            //ShowStatusMessage("Stopped record on exceeding max record duration:" + m_recordStorageFile.Path);

                            if (!m_mediaCaptureMgr.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported)
                            {
                                //if camera does not support record and Takephoto at the same time
                                //enable TakePhoto button again, after record finished

                                //will implement probably
                                //btnTakePhoto1.Content = "TakePhoto";


                                cantakephoto = true;
                            }
                        }
                        catch (Exception e)
                        {
                            //ShowExceptionMessage(e);
                        }
                    });
                }
            }

            internal void sldBrightness_ValueChanged(Object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
            {
                try
                {
                    bool succeeded = m_mediaCaptureMgr.VideoDeviceController.Brightness.TrySetValue(sldBrightness.Value);
                    if (!succeeded)
                    {
                        //ShowStatusMessage("Set Brightness failed");
                    }
                }
                catch (Exception exception)
                {
                    //ShowExceptionMessage(exception);
                }
            }
            internal void sldContrast_ValueChanged(Object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
            {
                try
                {
                    bool succeeded = m_mediaCaptureMgr.VideoDeviceController.Contrast.TrySetValue(sldContrast.Value);
                    if (!succeeded)
                    {
                        //ShowStatusMessage("Set Contrast failed");
                    }
                }
                catch (Exception exception)
                {
                    //ShowExceptionMessage(exception);
                }
            }
    }
}
