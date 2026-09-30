using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace opentuner.Utilities
{

    public class CustomGroupBox : GroupBox
    {

        public string _db_margin = "";

        // the tuner selected by a click on its video: the dashed border in the colour of the tuner instead of grey
        private Color _highlight_color = Color.Empty;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color HighlightColor
        {
            get { return _highlight_color; }
            set
            {
                if (_highlight_color == value)
                    return;

                _highlight_color = value;
                Invalidate();
            }
        }

        public CustomGroupBox() 
        {
            this.SetStyle(ControlStyles.UserPaint | 
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }


        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string db_margin
        {
            get { return this._db_margin; }
            set { _db_margin = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {

            Graphics g = e.Graphics;

            Color borderColor = Color.FromKnownColor(KnownColor.ControlDark);
            Color textColor = Color.FromKnownColor(KnownColor.ControlText);
            Font textFont = this.Font;

            SizeF textSize = g.MeasureString(this.Text, textFont);

            int padding = (int)(textSize.Height / 2);

            Rectangle bounds = new Rectangle(0,  padding , this.Width, this.Height - (padding * 2));

            if (_highlight_color.IsEmpty)
            {
                ControlPaint.DrawBorder(g, bounds, borderColor, ButtonBorderStyle.Dashed);
            }
            else
            {
                // twice as thick as the normal border so that it is seen at a glance
                Color c = _highlight_color;
                ControlPaint.DrawBorder(g, bounds,
                    c, 2, ButtonBorderStyle.Dashed, c, 2, ButtonBorderStyle.Dashed,
                    c, 2, ButtonBorderStyle.Dashed, c, 2, ButtonBorderStyle.Dashed);
            }

            Rectangle textBounds = new Rectangle(10, 0, (int)textSize.Width + 6, (int)textSize.Height);

            g.FillRectangle(new SolidBrush(Color.FromArgb(240, 240, 240)), textBounds);

            TextRenderer.DrawText(g, this.Text, textFont, new Point(10, 0), textColor);

            if (_db_margin.Length > 1)
            {
                Font db_margin_font = new Font(this.Font.FontFamily, 15f, FontStyle.Bold);

                SizeF db_margin_textSize = g.MeasureString(_db_margin.ToString(), db_margin_font);

                g.FillRectangle(new SolidBrush(Color.FromArgb(240, 240, 240)), new Rectangle(this.Width - (int)db_margin_textSize.Width - 15, 0, (int)db_margin_textSize.Width + 5, (int)db_margin_textSize.Height));
                TextRenderer.DrawText(g, _db_margin.ToString(), db_margin_font, new Point(this.Width - (int)db_margin_textSize.Width - 15, 0), textColor);
            }
        }
    }
}
