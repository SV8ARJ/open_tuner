using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Forms;
using System.Xml.Linq;
using System.Drawing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;
using Vortice.Direct2D1.Effects;
using Newtonsoft.Json.Linq;
using static opentuner.Utilities.DynamicPropertyMediaControls;

namespace opentuner.Utilities
{

    public class DynamicPropertyGroup
    {

        private delegate void UpdateTitleDelegate(CustomGroupBox group_box, Object obj);
        public delegate void SliderChanged(string key, int value);


        private Control _parent;

        //private GroupBox _groupBox;
        private CustomGroupBox _groupBox;
        private List<DynamicPropertyInterface> _items = new List<DynamicPropertyInterface>();

        public event SliderChanged OnSlidersChanged;
        public event ButtonPressedCallback OnMediaButtonPressed;

        private int _id = 0;

        public void setID(int id)
        {
            _id = id;
            _groupBox.Tag = id;
        }

        public int getID()
        {
            return _id;
        }

        // Moves this group below the groups already brought to the front (all groups are Dock=Top).
        public void BringToFront()
        {
            _groupBox.BringToFront();
        }

        public DynamicPropertyGroup(string GroupTitle, Control Parent)
        {
            _parent = Parent;


            // groupbox
            _groupBox = new CustomGroupBox();
            _groupBox.Dock = DockStyle.Top;
            _groupBox.AutoSize = true; 
            _groupBox.Text = GroupTitle;
            _groupBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));


            Parent.Controls.Add(_groupBox);
            Parent.Resize += _groupBox_Resize;

        }


        private void _groupBox_Resize(object sender, EventArgs e)
        {
            //_big_num_label.Top = 8;
            //_big_num_label.Left = _groupBox.Width - _big_num_label.Width ;
        }

        private void UpdateTitle(CustomGroupBox group_box, Object obj)
        {
            if (group_box == null)
                return;

            if (group_box.InvokeRequired)
            {
                UpdateTitleDelegate ulb = new UpdateTitleDelegate(UpdateTitle);
                try
                {
                    group_box.Invoke(ulb, new object[] { group_box, obj });
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException || ex is System.ComponentModel.InvalidAsynchronousStateException)
                {
                }
            }
            else
            {
                group_box.Text = obj.ToString();
            }
        }

        /*
        private void UpdateBigLabel(Label Lbl, Object obj)
        {
            if (Lbl == null)
                return;

            if (Lbl.InvokeRequired)
            {
                UpdateLabelDelegate ulb = new UpdateLabelDelegate(UpdateBigLabel);
                if (Lbl != null)
                {
                    Lbl?.Invoke(ulb, new object[] { Lbl, obj });
                }
            }
            else
            {
                Lbl.Text = obj.ToString();
            }
        }
        */


        public void UpdateBigLabel(string Text)
        {
            //UpdateBigLabel(_big_num_label, Text);
            _groupBox.db_margin = Text;
        }

        public void UpdateTitle(string Title)
        {
            UpdateTitle(_groupBox, Title);
        }

        public void AddItem(string Key, string Name)
        {
            _items.Add(new DynamicPropertyItem(_groupBox, Key, Name));
        }

        public void AddItem(string Key, string Name, Color color)
        {
            _items.Add(new DynamicPropertyItem(_groupBox, Key, Name, color));
        }

        public void AddItem(string Key, string Name, ContextMenuStrip MenuStrip)
        {
            var item = new DynamicPropertyItem(_groupBox, Key, Name, Color.Bisque);
            item.ContextMenu = MenuStrip;
            _items.Add(item);
        }

        public void AddSlider(string Key, string Name, int min, int max)
        {
            var item = new DynamicPropertySlider(_groupBox, Key, Name, min, max);
            item.OnSliderChanged += Item_OnSliderChanged;
            _items.Add(item);
        }

        void ButtonPressedCallback(string key, int function) // 0 = mute, 1 snapshot, 2 = record
        {
            OnMediaButtonPressed?.Invoke(key, function);
        }

        public void AddMediaControls(string Key, string Name)
        {
            var item = new DynamicPropertyMediaControls(_groupBox, Key, Name, ButtonPressedCallback);
            _items.Add(item);
        }

        private void Item_OnSliderChanged(string key, int value)
        {
            OnSlidersChanged?.Invoke(key, value);
        }

        public void UpdateColor(string Key, Color Col)
        {
            // this can probably be done more efficient, but will do for now
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    _items[c].UpdateColor(Col);
                    break;
                }
            }

        }

        // Shows the value of the item with this key in bold (not its title). Only for plain items.
        public void SetValueBold(string Key)
        {
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    (_items[c] as DynamicPropertyItem)?.SetValueBold();
                    break;
                }
            }
        }

        public void UpdateValue(string Key, string Value)
        {
            // this can probably be done more efficient, but will do for now
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    _items[c].UpdateValue(Value);
                    break;
                }
            }
        }

        public void UpdateMuteButtonColor(string Key, Color Col)
        {
            // this can probably be done more efficient, but will do for now
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    _items[c].UpdateMuteButtonColor(Col);
                    break;
                }
            }
        }

        public void UpdateRecordButtonColor(string Key, Color Col)
        {
            // this can probably be done more efficient, but will do for now
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    _items[c].UpdateRecordButtonColor(Col);
                    break;
                }
            }
        }

        public void SetSnapshotTooltip(string Key, string path)
        {
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    (_items[c] as DynamicPropertyMediaControls)?.SetSnapshotTooltip(path);
                    break;
                }
            }
        }

        public void SetRecordTooltip(string Key, string path)
        {
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    (_items[c] as DynamicPropertyMediaControls)?.SetRecordTooltip(path);
                    break;
                }
            }
        }

        public void ShowNotice(string Key, string text)
        {
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    _items[c].ShowNotice(text);
                    break;
                }
            }
        }

        public void UpdateStreamButtonColor(string Key, Color Col)
        {
            // this can probably be done more efficient, but will do for now
            for (int c = 0; c < _items.Count; c++)
            {
                if (_items[c].Key == Key)
                {
                    _items[c].UpdateStreamButtonColor(Col);
                    break;
                }
            }
        }

        public string GetValue(string key)
        {
            foreach (var item in _items)
            {
                if (item.Key == key)
                {
                    return item.LastValue;
                }
            }
            
            return ""; 
        }

        public Dictionary<string, string> GetAll()
        {
            Dictionary<string, string> data = new Dictionary<string, string>();

            foreach (var item in _items)
                data.Add(item.Key, item.LastValue);

            return data;
        }
    }
}
