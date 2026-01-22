using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace DarkSoulsOutlookToast
{
    public partial class ThisAddIn
    {
        // === ОБЯЗАТЕЛЬНО ===
        // Этот метод вызывается Designer'ом
        private void InternalStartup()
        {
            this.Startup += new EventHandler(ThisAddIn_Startup);
            this.Shutdown += new EventHandler(ThisAddIn_Shutdown);
        }

        // === STARTUP ===
        private void ThisAddIn_Startup(object sender, EventArgs e)
        {
            // Диагностика — можно удалить позже
            //MessageBox.Show("DarkSoulsOutlookToast loaded");

            // На всякий случай включаем попапы
            Properties.Settings.Default.PopupsEnabled = true;
            Properties.Settings.Default.Save();

            this.Application.NewMailEx += Application_NewMailEx;
            this.Application.ItemSend += Application_ItemSend;
        }

        // === SHUTDOWN ===
        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            this.Application.NewMailEx -= Application_NewMailEx;
            this.Application.ItemSend -= Application_ItemSend;
        }

        // === ВХОДЯЩЕЕ ПИСЬМО ===
        private void Application_NewMailEx(string entryIDCollection)
        {
            if (!Properties.Settings.Default.PopupsEnabled)
                return;

            ShowOverlay("ТЕБЕ ПИСЬМО", isIncoming: true);
            PlayWav("sound1.wav");
        }

        // === ОТПРАВКА ПИСЬМА ===
        private void Application_ItemSend(object item, ref bool cancel)
        {
            // Диагностика — убедиться, что событие реально срабатывает
            // MessageBox.Show("ItemSend fired");

            if (!Properties.Settings.Default.PopupsEnabled)
                return;

            ShowOverlay("ПИСЬМО ОТПРАВЛЕНО", isIncoming: false);
            PlayWav("sound2.wav");
        }

        // === ПРОИГРЫВАНИЕ ЗВУКА ===
        private void PlayWav(string fileName)
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    fileName
                );

                if (!File.Exists(path))
                    return;

                using (var player = new SoundPlayer(path))
                {
                    player.Play();
                }
            }
            catch
            {
                // НИЧЕГО НЕ ДЕЛАЕМ — Outlook нельзя ронять
            }
        }

        // === ОВЕРЛЕЙ DARK SOULS ===
        private void ShowOverlay(string text, bool isIncoming)
        {
            try
            {
                var thread = new Thread(() =>
                {
                    using (var form = new OverlayForm(text, isIncoming))
                    {
                        form.Shown += (s, e) => form.RunAutoClose();
                        form.ShowDialog();

                    }
                });

                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch
            {
                // Outlook важнее эффектов
            }
        }
    }
}
