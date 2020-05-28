
using System.Drawing;
using System.Windows.Forms;

namespace AmbientOcclusion
{
    public class Application
    {
        public const int Width = 1024;
        public const int Height = 1024;
        public const string WindowsName = "Ambient Occlusion";

        private Scene _graphicsDevice;
        private Form window;

        public void Run()
        {
            // Init Window
            window = new Form();            
            window.Size = new Size(Width, Height);
            window.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            window.Show();

            // Init DirectX12
            _graphicsDevice = new Scene(window);

            // MainLoop
            bool isClosing = false;
            window.FormClosing += (s, e) =>
            {
                isClosing = true;
            };

            while (!isClosing)
            {
                System.Windows.Forms.Application.DoEvents();

                _graphicsDevice.DrawFrame();
            }

            // Cleanup
            _graphicsDevice.Dispose();
            this.window.Dispose();
            this.window.Close();
        }      
    }
}
