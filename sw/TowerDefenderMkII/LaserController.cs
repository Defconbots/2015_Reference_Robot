using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FTD2XX_NET;

namespace TowerDefender
{
    public class LaserController
    {
        private SerialPort _port;
        private float _x = 100;
        private float _y = 100;
        private DateTime _lastFired = DateTime.MinValue;
        private bool _isFiring;
        private float gain = 0.5f;
        static FTDI _laser;
        private static bool laser_always_on = false;


        public void Connect(SerialPort port)
        {
            _port = port;
            port.Open();
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
        }

        private static void SendMessage()
        {
            if (!laser_always_on)
            {
                _laser.SetRTS(false);
                //Thread.Sleep(500);
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
        public void Update(float deltax, float deltay, bool shouldFire)
        {
            if (deltax > 0)
                _x += gain;
            if (deltax < 0)
                _x -= gain;

            if (deltay < 0)
                _y += gain;
            if (deltay > 0)
                _y -= gain;

            _x = Math.Min(Math.Max(_x, 0), 180);
            _y = Math.Min(Math.Max(_y, 0), 180);

            if (shouldFire && !_isFiring && (DateTime.Now - _lastFired).TotalMilliseconds > 500)
            {
                _isFiring = true;
                SendMessage();
                _lastFired = DateTime.Now;
            }

            if (_isFiring && (DateTime.Now - _lastFired).TotalMilliseconds > 500)
            {
                _isFiring = false;
            }

            var text = String.Format("{0:000},{1:000},{2}\r\n", _x, _y, _isFiring ? 1 : 0);
            //Thread.Sleep(100);
            _port.Write(text);
            var resp = _port.ReadExisting();
            
            //_controller.Update(0, 0, true);
        }
    }
}
