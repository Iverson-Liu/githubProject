using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ConfigTools
{
    public static class PreventTouchToMousePromotion
    {
        public static void Register(FrameworkElement root)
        {
            root.PreviewMouseDown += Evaluate;
            root.MouseDown += Evaluate;
            root.MouseUp += Evaluate;
        }
        private static void Evaluate(object sender, MouseEventArgs e)
        {
            if (e.StylusDevice!=null)
            {
                e.Handled = true;
            }
        }
    }
}
