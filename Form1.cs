using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace POS_Updater
{

    public partial class Form1 : Form
    {
        string POSexePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft Dynamics AX\60\Retail POS\POS.exe";

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse);

       public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
           
        }
       

       private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            
            FileProperties fpr = new FileProperties();
            
            progressBar1.Increment(5);

            Process proceso = new Process();
            proceso.StartInfo.FileName = POSexePath;

            if (progressBar1.Value==100)
            {
                timer1.Enabled = false;
                Form2 form = new Form2();
                form.Show();
                proceso.Start();
                Application.Exit();
            }
        }
    }
}
