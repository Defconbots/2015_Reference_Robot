using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using FTD2XX_NET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Input = Microsoft.Xna.Framework.Input; // to provide shorthand to clear up ambiguities


namespace TowerDefender
{
    public partial class Form1 : Form
    {
        VideoCaptureDevice videoSource;
        FilterInfoCollection videoDevices;
        private LaserController _controller;
        FrameProcessor processor;
        static FTDI _laser;
        GamePadState _gp;

        public Form1()
        {
            InitializeComponent();

            _controller = new LaserController();
            _laser = new FTDI();
            //uint devs =0 ;
            //_laser.GetNumberOfDevices(ref devs);
            // FTDI.FT_DEVICE_INFO_NODE[] list = new FTDI.FT_DEVICE_INFO_NODE[devs];
            //_laser.GetDeviceList(list);
            _laser.OpenBySerialNumber("DEFCON01");
            _laser.SetBaudRate(4800);
            _laser.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_1, FTDI.FT_PARITY.FT_PARITY_NONE);
            _laser.SetRTS(true);
            processor = new FrameProcessor(_controller);


        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                comboBox1.Items.Clear();
                if (videoDevices.Count == 0)
                    throw new ApplicationException();

                foreach (FilterInfo device in videoDevices)
                {
                    comboBox1.Items.Add(device.Name);
                }
                comboBox1.SelectedIndex = 0; //make dafault to first cam
            }
            catch (ApplicationException)
            {
                comboBox1.Items.Add("No capture device on your system");
            }

            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                comboBox2.Items.Add(port);
            }
            comboBox2.SelectedIndex = 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _controller.Connect(new SerialPort((string)comboBox2.SelectedItem, 57600));

            videoSource = new VideoCaptureDevice(videoDevices[comboBox1.SelectedIndex].MonikerString);
            videoSource.NewFrame += new NewFrameEventHandler(video_NewFrame);
            videoSource.Start();
        }
        
        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone();
            Bitmap scaled_bitmap = new Bitmap(bitmap, 1024, 768);
            var processed = processor.ProcessFrame((Bitmap)eventArgs.Frame.Clone());

            try
            {
                pictureBox1.Invoke((Action)(() =>
                {
                    pictureBox1.Width = scaled_bitmap.Width;
                    pictureBox1.Height = scaled_bitmap.Height;
                    pictureBox1.Image = scaled_bitmap;
                    Width = pictureBox1.Width + pictureBox1.Left;
                    Height = pictureBox1.Height + pictureBox1.Top;
                }));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (videoSource != null)
            {
                videoSource.SignalToStop();
            }
        }


        private void OnSettingsClick(object sender, EventArgs e)
        {
            if ((videoSource != null) && (videoSource is VideoCaptureDevice))
            {
                try
                {
                    ((VideoCaptureDevice)videoSource).DisplayPropertyPage(this.Handle);
                }
                catch (NotSupportedException ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _controller.Update(0,-0.02, false);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            _controller.Update(-0.02,0, false);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            _controller.Update(0.02, 0, false);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            _controller.Update(0, 0.02, false);
        }

        private static void SendMessage()
        {
            _laser.SetRTS(false);
            Thread.Sleep(300);
            uint dummy = 0;
            string msg = "[*I05]";
            _laser.Write(msg, msg.Length, ref dummy);
            Thread.Sleep(100);
            _laser.Write(msg, msg.Length, ref dummy);
            Thread.Sleep(100);
            _laser.Write(msg, msg.Length, ref dummy);
            Thread.Sleep(100);
            _laser.SetRTS(true);
            //_controller.Update(0, 0, true);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Thread SendMessageThread = new Thread(SendMessage);
            SendMessageThread.Start();

        }

        private void button8_Click(object sender, EventArgs e)
        {
            _controller.AbsPosition(100, 100);
        }

        private void gamepad_poll_Tick(object sender, EventArgs e)
        {
            _gp = GamePad.GetState(0);
            if (_gp.Buttons.A == Input.ButtonState.Pressed || _gp.Buttons.RightShoulder == Input.ButtonState.Pressed)
            {
                Thread SendMessageThread = new Thread(new ThreadStart(SendMessage));
                SendMessageThread.Start();
            }
            if (_controller.isOpen)
            {
                _controller.AbsPosition((int)((_gp.ThumbSticks.Left.X * 90) + 90), (int)((_gp.ThumbSticks.Left.Y * 90) + 90));
            }
                
        }
    }
}

