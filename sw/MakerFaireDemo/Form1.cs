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
        private static bool laser_always_on = false;
        private SerialPort serial = new SerialPort("COM22", 57600, Parity.None, 8, StopBits.One);

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
            if (laser_always_on)
            {
                _laser.SetRTS(false);
            }
            else
            {
                _laser.SetRTS(true);
            }
            processor = new FrameProcessor(_controller);
        }

        private bool InitTrain()
        {
            serial.Open();
            Thread.Sleep(100);
            serial.Write("{0V}");
            while (serial.BytesToRead < 4) ;
            string response = serial.ReadExisting();
            if (response[0] != '(' || response[3] != ')')
            {
                return false;
            }
            // turn on all the LEDs
            for (int i = 1; i < 4; i++)
            {
                serial.Write("[" + i.ToString() + "B99]");
                Thread.Sleep(500);
            }
            return true;
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
                comboBox1.SelectedIndex = 1; //make dafault to first cam
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
            comboBox2.SelectedIndex = 1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _controller.Connect(new SerialPort((string)comboBox2.SelectedItem, 57600));

            videoSource = new VideoCaptureDevice(videoDevices[comboBox1.SelectedIndex].MonikerString);
            videoSource.NewFrame += new NewFrameEventHandler(video_NewFrame);
            videoSource.Start();
            //score_timer.Enabled = true;
            gamepad_poll.Enabled = true;
            InitTrain();
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
            for (int i = 1; i < 4; i++)
            {
                serial.Write("[" + i.ToString() + "B00]");
                Thread.Sleep(500);
                serial.Write("[" + i.ToString() + "R00]");
                Thread.Sleep(500);
            }
            serial.Close();
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
            if (!laser_always_on)
            {
                _laser.SetRTS(false);
                Thread.Sleep(500);
            }
            uint dummy = 0;
            string msg = "[*I05]";
            _laser.Write(msg, msg.Length, ref dummy);
            Thread.Sleep(100);
            _laser.Write(msg, msg.Length, ref dummy);
            Thread.Sleep(100);
            _laser.Write(msg, msg.Length, ref dummy);
            Thread.Sleep(300);
            if (!laser_always_on)
            {
                _laser.SetRTS(true);
            }
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

        private double _x_pos = 90;
        private double _y_pos = 90;
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
                // joe's bad code
                //int x = (int)(_gp.ThumbSticks.Left.X * 90) + 90;
                //int y = (int)(_gp.ThumbSticks.Left.Y * 90) + 90;
                //if (x < _x_pos && _x_pos > 60) _x_pos -= 1;
                //else if (x > _x_pos && _x_pos < 120) _x_pos += 1;
                //if (y < _y_pos) _y_pos -= 1;
                //else if (y > _y_pos && _y_pos < 100) _y_pos += 1;
                ////_controller.AbsPosition(_x_pos, _y_pos);
                ////if (x > 120) x = 120;
                ////else if (x < 60) x = 60;
                ////if (y > 110) y = 110;
                ////_controller.AbsPosition(x, y);

                var speedDegreesPerSecond = 15.0;
                var x = _gp.ThumbSticks.Left.X;
                var y = _gp.ThumbSticks.Left.Y;
                _x_pos += x * (gamepad_poll.Interval / 1000.0) * speedDegreesPerSecond;
                _y_pos += y * (gamepad_poll.Interval / 1000.0) * speedDegreesPerSecond;
                _x_pos = Math.Max(Math.Min(_x_pos, 120), 60);
                _y_pos = Math.Max(Math.Min(_y_pos, 110), 60);
                _controller.AbsPosition((int)_x_pos, (int)_y_pos);
            }
        }

        private void Hit1()
        {
            serial.Write("[1B00]");
            Thread.Sleep(250);
            serial.Write("[1R99]");
            Thread.Sleep(1000);
            serial.Write("[1R00]");
            Thread.Sleep(500);
            serial.Write("[1B99]");
            Thread.Sleep(50);
            hit_1 = false;
        }
        private void Hit2()
        {
            serial.Write("[2B00]");
            Thread.Sleep(250);
            serial.Write("[2R99]");
            Thread.Sleep(1000);
            serial.Write("[2R00]");
            Thread.Sleep(500);
            serial.Write("[2B99]");
            Thread.Sleep(50);
            hit_2 = false;
        }
        private void Hit3()
        {
            serial.Write("[3B00]");
            Thread.Sleep(250);
            serial.Write("[3R99]");
            Thread.Sleep(1000);
            serial.Write("[3R00]");
            Thread.Sleep(500);
            serial.Write("[3B99]");
            Thread.Sleep(50);
            hit_3 = false;
        }

        private bool hit_1 = false;
        private bool hit_2 = false;
        private bool hit_3 = false;

        private void score_timer_Tick(object sender, EventArgs e)
        {
            string response;
            serial.DiscardInBuffer();
            
            serial.Write("{1I}");
            Thread.Sleep(100);
            //while (serial.BytesToRead < 4) ;
            response = serial.ReadExisting();
            if (response.Length > 3 && response[2] == '5')
            {
                if (hit_1 == false)
                {
                    hit_1 = true;
                    serial.Write("[1I00]");
                    Thread.Sleep(50);
                    Thread hit_thread = new Thread(new ThreadStart(Hit1));
                    hit_thread.Start();
                }
            }

            serial.Write("{2I}");
            Thread.Sleep(100);
            //while (serial.BytesToRead < 4) ;
            response = serial.ReadExisting();
            if (response.Length > 3 && response[2] == '5')
            {
                if (hit_2 == false)
                {
                    hit_2 = true;
                    serial.Write("[2I00]");
                    Thread.Sleep(50);
                    Thread hit_thread = new Thread(new ThreadStart(Hit2));
                    hit_thread.Start();
                }
            }

            serial.Write("{3I}");
            Thread.Sleep(100);
            //while (serial.BytesToRead < 4) ;
            response = serial.ReadExisting();
            if (response.Length > 3 && response[2] == '5')
            {
                if (hit_3 == false)
                {
                    hit_3 = true;
                    serial.Write("[3I00]");
                    Thread.Sleep(50);
                    Thread hit_thread = new Thread(new ThreadStart(Hit3));
                    hit_thread.Start();
                }
            }
        }
    }
}

