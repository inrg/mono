// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
//
// Authors:
//	Peter Bartok	pbartok@novell.com
//
//

// NOT COMPLETE

using System.Collections;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms {
	[DesignTimeVisible(false)]
	[ToolboxItem(false)]
	public sealed class MdiClient : Control {
		#region Local Variables
		private int mdi_created;
		private HScrollBar hbar;
		private VScrollBar vbar;
		private SizeGrip sizegrip;
		private int hbar_value;
		private int vbar_value;
		private bool lock_sizing;
		private int prev_bottom;
		private LayoutEventHandler initial_layout_handler;
		private bool setting_windowstates = false;
		internal ArrayList mdi_child_list;
        private string form_text;
        private bool setting_form_text;

		#endregion	// Local Variables

		#region Public Classes
		public new class ControlCollection : Control.ControlCollection {

			private MdiClient owner;
			
			public ControlCollection(MdiClient owner) : base(owner) {
				this.owner = owner;
				owner.mdi_child_list = new ArrayList ();
			}

			public override void Add(Control value) {
				if ((value is Form) == false || !(((Form)value).IsMdiChild)) {
					throw new ArgumentException("Form must be MdiChild");
				}
				owner.mdi_child_list.Add (value);
				base.Add (value);

				// newest member is the active one
				Form form = (Form) value;
				owner.ActiveMdiChild = form;
			}

			public override void Remove(Control value)
			{
				Form form = value as Form;
				if (form != null) {
					MdiWindowManager wm = form.WindowManager as MdiWindowManager;
					if (wm != null) {
						form.Closed -= wm.form_closed_handler;
					}
				}

				owner.mdi_child_list.Remove (value);
				base.Remove (value);
			}
		}
		#endregion	// Public Classes

		#region Public Constructors
		public MdiClient()
		{
			BackColor = SystemColors.AppWorkspace;
			Dock = DockStyle.Fill;
			SetStyle (ControlStyles.Selectable, false);
		}
		#endregion	// Public Constructors


        internal void SetParentText(bool text_changed)
        {
            if (setting_form_text)
                return;

            setting_form_text = true;

            if (text_changed)
                form_text = ParentForm.Text;

            if (ParentForm.ActiveMaximizedMdiChild == null)
            {
                ParentForm.Text = form_text;
            }
            else
            {
                ParentForm.Text = form_text + " - [" + ParentForm.ActiveMaximizedMdiChild.form.Text + "]";
            }

            setting_form_text = false;
        }

		internal override void OnPaintBackgroundInternal (PaintEventArgs pe)
		{
			if (BackgroundImage != null)
				return;

			if (Parent == null || Parent.BackgroundImage == null)
				return;
			Parent.PaintControlBackground (pe);
		}

		internal Form ParentForm {
			get { return (Form) Parent; }
		}

		protected override Control.ControlCollection CreateControlsInstance ()
		{
			return new MdiClient.ControlCollection (this);
		}

		protected override void WndProc(ref Message m) {
			/*
			switch ((Msg) m.Msg) {
				case Msg.WM_PAINT: {				
					Console.WriteLine ("ignoring paint");
					return;
				}
			}
			*/
			base.WndProc (ref m);
		}

		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);

			// Should probably make this into one loop
			SizeScrollBars ();
			ArrangeWindows ();
		}

		protected override void ScaleCore (float dx, float dy)
		{
			base.ScaleCore (dx, dy);
		}

		protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
		{
			base.SetBoundsCore (x, y, width, height, specified);
		}

		#region Public Instance Properties
		[Localizable(true)]
		public override System.Drawing.Image BackgroundImage {
			get {
				return base.BackgroundImage;
			}
			set {
				base.BackgroundImage = value;
			}
		}

		public Form [] MdiChildren {
			get {
				if (mdi_child_list == null)
					return new Form [0];
				return (Form []) mdi_child_list.ToArray (typeof (Form));
			}
		}
		#endregion	// Public Instance Properties

#region Protected Instance Properties
		protected override CreateParams CreateParams {
			get {
				return base.CreateParams;
			}
		}
		#endregion	// Protected Instance Properties

		#region Public Instance Methods
		public void LayoutMdi (MdiLayout value) {

			int max_width = Int32.MaxValue;
			int max_height = Int32.MaxValue;

			if (Parent != null) {
				max_width = Parent.Width;
				max_height = Parent.Height;
			}

			switch (value) {
			case MdiLayout.Cascade:
				int i = 0;
				for (int c = Controls.Count - 1; c >= 0; c--) {
					Form form = (Form) Controls [c];

					int l = 22 * i;
					int t = 22 * i;

					if (i != 0 && (l + form.Width > max_width || t + form.Height > max_height)) {
						i = 0;
						l = 22 * i;
						t = 22 * i;
					}

					form.Left = l;
					form.Top = t;

					i++;
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}
		#endregion	// Public Instance Methods

		#region Protected Instance Methods
		#endregion	// Protected Instance Methods

		internal void SizeScrollBars ()
		{
			if (lock_sizing)
				return;

			if (Controls.Count == 0 || ((Form) Controls [0]).WindowState == FormWindowState.Maximized) {
				if (hbar != null)
					hbar.Visible = false;
				if (vbar != null)
					vbar.Visible = false;
				return;
			}
				
			bool hbar_required = false;
			bool vbar_required = false;

			int right = 0;
            int left = 0;
            int top = 0;
            int bottom = 0;

			foreach (Form child in Controls) {
				if (!child.Visible)
					continue;
				if (child.Right > right)
					right = child.Right;
				if (child.Left < left) {
					hbar_required = true;
					left = child.Left;
                }

                if (child.Bottom > bottom)
                    bottom = child.Bottom;
                if (child.Top < 0) {
                    vbar_required = true;
                    top = child.Top;
                }
			}


            int first_right = Width;
            int first_bottom = Height;
            int right_edge = first_right;
            int bottom_edge = first_bottom;
			int prev_right_edge;
			int prev_bottom_edge;

			bool need_hbar = false;
			bool need_vbar = false;

			do {
				prev_right_edge = right_edge;
				prev_bottom_edge = bottom_edge;

				if (hbar_required || right > right_edge) {
					need_hbar = true;
                    bottom_edge = first_bottom - SystemInformation.HorizontalScrollBarHeight;
				} else {
					need_hbar = false;
                    bottom_edge = first_bottom;
				}

				if (vbar_required || bottom > bottom_edge) {
					need_vbar = true;
                    right_edge = first_right - SystemInformation.VerticalScrollBarWidth;
				} else {
					need_vbar = false;
                    right_edge = first_right;
				}

			} while (right_edge != prev_right_edge || bottom_edge != prev_bottom_edge);

			if (need_hbar) {
				if (hbar == null) {
					hbar = new HScrollBar ();
					Controls.AddImplicit (hbar);
				}
				hbar.Visible = true;
				CalcHBar (left, right, right_edge, need_vbar);
			} else if (hbar != null)
				hbar.Visible = false;

			if (need_vbar) {
				if (vbar == null) {
					vbar = new VScrollBar ();
					Controls.AddImplicit (vbar);
				}
				vbar.Visible = true;
				CalcVBar (top, bottom, bottom_edge, need_hbar);
			} else if (vbar != null)
				vbar.Visible = false;

			if (need_hbar && need_vbar) {
				if (sizegrip == null) {
					sizegrip = new SizeGrip ();
					Controls.AddImplicit (sizegrip);
				}
				sizegrip.Location = new Point (hbar.Right, vbar.Bottom);
				sizegrip.Width = vbar.Width;
				sizegrip.Height = hbar.Height;
				sizegrip.Visible = true;
			} else if (sizegrip != null) {
				sizegrip.Visible = false;
			}
		}

		private void CalcHBar (int left, int right, int right_edge, bool vert_vis)
		{
			int virtual_left = Math.Min (left, 0);
			int virtual_right = Math.Max (right, right_edge);
			int diff = (virtual_right - virtual_left) - right_edge;
			hbar.Left = 0;
			hbar.Top = Height - hbar.Height;
			hbar.Width = Width - (vert_vis ? SystemInformation.VerticalScrollBarWidth : 0);
			hbar.LargeChange = 50;
			hbar.Maximum = diff + 51;
			hbar.Value = -virtual_left;

			hbar.ValueChanged += new EventHandler (HBarValueChanged);
		}

		private void CalcVBar (int top, int bottom, int bottom_edge, bool horz_vis)
		{
			int virtual_top = Math.Min (top, 0);
			int virtual_bottom = Math.Max (bottom, bottom_edge);
			int diff = (virtual_bottom - virtual_top) - bottom_edge;
			vbar.Top = 0;
			vbar.Left = Width - vbar.Width;
			vbar.Height = Height - (horz_vis ? SystemInformation.HorizontalScrollBarHeight : 0);
			vbar.LargeChange = 50;
			vbar.Maximum = diff + 51;
			vbar.Value = -virtual_top;
			vbar.ValueChanged += new EventHandler (VBarValueChanged);
			
		}

		private void HBarValueChanged (object sender, EventArgs e)
		{
			if (hbar.Value == hbar_value)
				return;

			lock_sizing = true;

			try {
				foreach (Form child in Controls) {
					child.Left += hbar_value - hbar.Value;
				}
			} finally {
				lock_sizing = false;
			}

			hbar_value = hbar.Value;
			lock_sizing = false;
		}

		private void VBarValueChanged (object sender, EventArgs e)
		{
			if (vbar.Value == vbar_value)
				return;

			lock_sizing = true;

			try {
				foreach (Form child in Controls) {
					child.Top += vbar_value - vbar.Value;
				}
			} finally {
				lock_sizing = false;
			}

			vbar_value = vbar.Value;
			lock_sizing = false;
		}

		private void ArrangeWindows ()
		{
			int change = 0;
			if (prev_bottom != -1)
				change = Bottom - prev_bottom;

			foreach (Control c in Controls) {
				Form child = c as Form;

				if (c == null || !child.Visible)
					continue;

				MdiWindowManager wm = child.WindowManager as MdiWindowManager;
				if (wm.GetWindowState () == FormWindowState.Maximized)
					wm.SizeMaximized ();

				if (wm.GetWindowState () == FormWindowState.Minimized) {
					child.Top += change;
				}
					
			}

			prev_bottom = Bottom;
		}

		private void FormLocationChanged (object sender, EventArgs e)
		{
			SizeScrollBars ();
		}

		private int iconic_x = -1;
		private int iconic_y = -1;
		internal void ArrangeIconicWindows ()
		{
			int xspacing = 160;
			int yspacing = 25;

			if (iconic_x == -1 && iconic_y == -1) {
				iconic_x = Left;
				iconic_y = Bottom - yspacing;
			}

			lock_sizing = true;
			foreach (Form form in Controls) {
				if (form.WindowState != FormWindowState.Minimized)
					continue;

				MdiWindowManager wm = (MdiWindowManager) form.WindowManager;
				// Need to get the width in the loop cause some themes might have
				// different widths for different styles
				int bw = ThemeEngine.Current.ManagedWindowBorderWidth (wm);
				
				if (wm.IconicBounds != Rectangle.Empty) {
					form.Bounds = wm.IconicBounds;
					continue;
				}
					
				// The extra one pixel is a cheap hack for now until we
				// handle 0 client sizes properly in the driver
				int height = wm.TitleBarHeight + (bw * 2) + 1; 
				// The extra one pixel here is to avoid scrollbars
				Rectangle rect = new Rectangle (iconic_x, iconic_y - 1, xspacing, height);
				form.Bounds = wm.IconicBounds = rect;

				iconic_x += xspacing;
				if (iconic_x >= Right) {
					iconic_x = Left;
					iconic_y -= height;
				}
			}
			lock_sizing = false;
		}

		internal void CloseChildForm (Form form)
		{
			if (Controls.Count > 1) {
				Form next = (Form) Controls [1];
				if (form.WindowState == FormWindowState.Maximized)
					next.WindowState = FormWindowState.Maximized;
				ActivateChild (next);
			}

			Controls.Remove (form);
			form.Close ();
		}

		internal void ActivateNextChild ()
		{
			if (Controls.Count < 1)
				return;

			Form front = (Form) Controls [0];
			Form form = (Form) Controls [1];

			front.SendToBack ();
			ActivateChild (form);
		}

		internal void ActivateChild (Form form)
		{
			if (Controls.Count < 1)
				return;

			Form current = (Form) Controls [0];

			form.BringToFront ();

			if (current != form) {
				Message m = new Message ();
				m.Msg = (int) Msg.WM_NCPAINT;
				m.HWnd = current.Handle;
				m.LParam = IntPtr.Zero;
				m.WParam = new IntPtr (1);
				XplatUI.SendMessage (ref m);

				m.HWnd = form.Handle;
				XplatUI.SendMessage (ref m);
				this.SetWindowStates ((MdiWindowManager) form.window_manager, form.WindowState);
			}
		}
		
		internal bool SetWindowStates (MdiWindowManager wm, FormWindowState window_state)
		{
		/*
			MDI WindowState behaviour:
			- If the active window is maximized, all other maximized windows are normalized.
			- If a normal window gets focus and the original active window was maximized, 
			  the normal window gets maximized and the original window gets normalized.
			- If a minimized window gets focus and the original window was maximized, 
			  the minimzed window gets maximized and the original window gets normalized. 
			  If the ex-minimized window gets deactivated, it will be normalized.
		*/
			Form form = wm.form;

			if (setting_windowstates) {
				return false;
			}
			
			if (!form.Visible)
				return false;
			
			bool is_active = wm.IsActive();
			bool maximize_this = false;
			
			if (!is_active){
				return false;
			}

			setting_windowstates = true;
			foreach (Form frm in mdi_child_list) {
				if (frm == form) {
					continue;
				} else if (!frm.Visible){
					continue;
				}
				if (frm.WindowState == FormWindowState.Maximized && is_active) {
					if (!maximize_this && is_active)
						maximize_this = true;	
					frm.WindowState = FormWindowState.Normal;
				}
			}
			if (maximize_this) {
			    form.WindowState = FormWindowState.Maximized;
            }
            SetParentText(false);
            SizeScrollBars();
            XplatUI.RequestNCRecalc(ParentForm.Handle);

			setting_windowstates = false;

            return maximize_this;
		}

		internal int ChildrenCreated {
			get { return mdi_created; }
			set { mdi_created = value; }
		}

		internal Form ActiveMdiChild {
			get {
				if (Controls.Count < 1)
					return null;
				return (Form) Controls [0];
			}
			set {
				ActivateChild (value);
			}
		}
	}
}

