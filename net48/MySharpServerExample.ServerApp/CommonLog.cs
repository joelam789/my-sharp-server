using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using MySharpServer.Common;
using MySharpServer.Framework;

namespace MySharpServerExample.ServerApp
{
    public static class CommonLog
    {
        static readonly ServerFormLogger m_Logger = null;

        static CommonLog()
        {
            m_Logger = new ServerFormLogger();
        }

        public static void SetGuiControl(Form form, RichTextBox textbox)
        {
            if (m_Logger != null) m_Logger.SetGuiControl(form, textbox);
        }

        public static IServerLogger GetLogger()
        {
            return m_Logger;
        }

        public static void Info(string msg)
        {
            if (m_Logger != null) m_Logger.Info(msg);
        }

        public static void Debug(string msg)
        {
            if (m_Logger != null) m_Logger.Debug(msg);
        }

        public static void Warn(string msg)
        {
            if (m_Logger != null) m_Logger.Warn(msg);
        }

        public static void Error(string msg)
        {
            if (m_Logger != null) m_Logger.Error(msg);
        }
    }

    public class ServerFormLogger : ServerLogger
    {
        Form m_Form = null;
        RichTextBox m_TextBox = null;

        public void SetGuiControl(Form form, RichTextBox textbox)
        {
            m_Form = form;
            m_TextBox = textbox;
        }

        private void TryToSendLogToGui(string msg)
        {
            Form form = m_Form;
            RichTextBox textbox = m_TextBox;
            try
            {
                if (form != null && textbox != null)
                {
                    form.BeginInvoke((Action)(() =>
                    {
                        if (textbox.Lines.Length > 256)
                        {
                            List<string> finalLines = textbox.Lines.ToList();
                            finalLines.RemoveRange(0, 128);
                            textbox.Lines = finalLines.ToArray();
                        }
                        textbox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "]" + msg + Environment.NewLine);
                        textbox.SelectionStart = textbox.Text.Length;
                        textbox.ScrollToCaret();
                    }));
                }
            }
            catch { }
        }

        public override void Info(string msg)
        {
            base.Info(msg);
            TryToSendLogToGui("[INFO] " + msg);
        }

        public override void Debug(string msg)
        {
            base.Debug(msg);
            TryToSendLogToGui("[DEBUG] " + msg);
        }

        public override void Warn(string msg)
        {
            base.Warn(msg);
            TryToSendLogToGui("[WARN] " + msg);
        }

        public override void Error(string msg)
        {
            base.Error(msg);
            TryToSendLogToGui("[ERROR] " + msg);
        }
    }
}
