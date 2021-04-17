/*
Copyright (C) 2020-2021 Tris Shores

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyKeyFile(@"..\..\signing\keyFile.snk")]

namespace viewmarc_ui
{
    internal static class c_Ui
    {
        #region declarations

        internal static c_FrmMain v_frmMain;

        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] v_args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (v_args.Length == 1 && File.Exists(v_args[0])) v_frmMain = new c_FrmMain(v_mrcOpenFilePathArg: v_args[0]);
            else v_frmMain = new c_FrmMain();

            using (v_frmMain)
            {
                Application.Run(v_frmMain);
            }
        }
    }
}
