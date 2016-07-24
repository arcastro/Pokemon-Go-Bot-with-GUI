using System;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Exceptions;
using System.Windows.Forms;

namespace PokemonGo.RocketAPI.GUI
{
    class Program
    {

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}