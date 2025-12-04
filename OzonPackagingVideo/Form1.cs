using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace OzonPackagingVideo
{
    public partial class Form1 : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private bool isRecording = false;
        private DateTime recordingStartTime;
        private string currentOrderNumber = "";
        private bool isClosing = false;
        private string lastScannedCode = "";
        private ComboBox cmbCameras;
        private Button btnConnect;
        private PictureBox videoPreview;
        //private Button btnScanOrder;
        //private Button btnScanLabel;
        private Label lblStatus;
        private Label lblTimer;
        private System.Windows.Forms.Timer timer;
        private Panel rightPanel;

        private Label lblOrderNumber;
        private TextBox txtOrderNumber;

        public Form1()
        {
            InitializeComponent();
            SetupForm();
        }

        private void SetupForm()
        {
            this.Text = "ТЕСТ: Видеофиксация упаковки";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // Создаем папку для записей
            string recordingsPath = Path.Combine(Application.StartupPath, "TestRecordings");
            if (!Directory.Exists(recordingsPath))
                Directory.CreateDirectory(recordingsPath);

            InitializeComponents();
            FindCameras();
        }

        private void InitializeComponents()
        {
            // -----------------------
            // ПРАВАЯ ПАНЕЛЬ
            // -----------------------
            rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Right;
            rightPanel.Width = 300;
            rightPanel.BackColor = Color.LightGray;
            this.Controls.Add(rightPanel);

            int margin = 10;
            int currentY = 10;

            // --- ComboBox для выбора камеры ---
            cmbCameras = new ComboBox();
            cmbCameras.Location = new Point(margin, currentY);
            cmbCameras.Size = new Size(rightPanel.Width - 2 * margin, 21);
            rightPanel.Controls.Add(cmbCameras);

            currentY += 30;

            // --- Кнопка подключения камеры ---
            btnConnect = new Button();
            btnConnect.Location = new Point(margin, currentY);
            btnConnect.Size = new Size(rightPanel.Width - 2 * margin, 30);
            btnConnect.Text = "Подключить камеру";
            btnConnect.Click += BtnConnect_Click;
            rightPanel.Controls.Add(btnConnect);

            currentY += 40;

            // --- Поле для номера заказа (сканер штрих-кода) ---
            lblOrderNumber = new Label();
            lblOrderNumber.Text = "Номер заказа (сканер):";
            lblOrderNumber.Location = new Point(margin, currentY);
            lblOrderNumber.Size = new Size(rightPanel.Width - 2 * margin, 15);
            rightPanel.Controls.Add(lblOrderNumber);

            currentY += 18;

            txtOrderNumber = new TextBox();
            txtOrderNumber.Location = new Point(margin, currentY);
            txtOrderNumber.Size = new Size(rightPanel.Width - 2 * margin, 20);
            txtOrderNumber.KeyDown += TxtOrderNumber_KeyDown;   // обработчик Enter
            rightPanel.Controls.Add(txtOrderNumber);

            currentY += 30;

            // --- Кнопка "СКАНИРОВАТЬ ЗАКАЗ" ---
            //btnScanOrder = new Button();
            //btnScanOrder.Location = new Point(margin, currentY);
            //btnScanOrder.Size = new Size(rightPanel.Width - 2 * margin, 40);
            //btnScanOrder.Text = "СКАНИРОВАТЬ ЗАКАЗ";
            //btnScanOrder.BackColor = Color.LightGreen;
            //btnScanOrder.Enabled = false;
            //btnScanOrder.Click += BtnScanOrder_Click;
            //rightPanel.Controls.Add(btnScanOrder);

            //currentY += 50;

            // --- Кнопка "СКАНИРОВАТЬ ЭТИКЕТКУ" ---
            //btnScanLabel = new Button();
            //btnScanLabel.Location = new Point(margin, currentY);
            //btnScanLabel.Size = new Size(rightPanel.Width - 2 * margin, 40);
            //btnScanLabel.Text = "СКАНИРОВАТЬ ЭТИКЕТКУ";
            //btnScanLabel.BackColor = Color.LightCoral;
            //btnScanLabel.Enabled = false;
            //btnScanLabel.Click += BtnScanLabel_Click;
            //rightPanel.Controls.Add(btnScanLabel);

            //currentY += 60;

            // --- Статус ---
            lblStatus = new Label();
            lblStatus.Location = new Point(margin, currentY);
            lblStatus.Size = new Size(rightPanel.Width - 2 * margin, 40);
            lblStatus.Text = "Статус: Отключено";
            lblStatus.ForeColor = Color.Red;
            lblStatus.AutoSize = false;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            rightPanel.Controls.Add(lblStatus);

            currentY += 50;

            // --- Таймер ---
            lblTimer = new Label();
            lblTimer.Location = new Point(margin, currentY);
            lblTimer.Size = new Size(rightPanel.Width - 2 * margin, 30);
            lblTimer.Text = "00:00:00";
            lblTimer.Font = new Font("Arial", 14, FontStyle.Bold);
            lblTimer.ForeColor = Color.Red;
            lblTimer.Visible = false;
            rightPanel.Controls.Add(lblTimer);

            // -----------------------
            // ОБЛАСТЬ ВИДЕО СЛЕВА
            // -----------------------
            videoPreview = new PictureBox();
            videoPreview.Dock = DockStyle.Fill;
            videoPreview.BorderStyle = BorderStyle.FixedSingle;
            videoPreview.BackColor = Color.Black;
            videoPreview.SizeMode = PictureBoxSizeMode.Zoom;
            this.Controls.Add(videoPreview);

            // Таймер
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;

            // Обработка закрытия формы
            this.FormClosing += Form1_FormClosing;
        }

        private void FindCameras()
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                cmbCameras.Items.Clear();

                if (videoDevices.Count == 0)
                {
                    cmbCameras.Items.Add("Камеры не найдены");
                    cmbCameras.SelectedIndex = 0;
                    btnConnect.Enabled = false;
                }
                else
                {
                    foreach (FilterInfo device in videoDevices)
                    {
                        cmbCameras.Items.Add(device.Name);
                    }
                    cmbCameras.SelectedIndex = 0;
                    btnConnect.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при поиске камер: " + ex.Message);
            }
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (videoSource != null && videoSource.IsRunning)
                {
                    // Отключаем
                    videoSource.SignalToStop();
                    videoSource.NewFrame -= VideoSource_NewFrame;
                    videoSource = null;

                    lblStatus.Text = "Статус: Камера отключена";
                    lblStatus.ForeColor = Color.Red;

                    btnConnect.Text = "Подключить камеру";
                    //btnScanOrder.Enabled = false;
                    //btnScanLabel.Enabled = false;
                    return;
                }

                if (videoDevices == null || videoDevices.Count == 0)
                {
                    MessageBox.Show("Камеры не найдены.");
                    return;
                }

                int index = cmbCameras.SelectedIndex;
                if (index < 0 || index >= videoDevices.Count)
                {
                    MessageBox.Show("Выберите камеру из списка.");
                    return;
                }

                videoSource = new VideoCaptureDevice(videoDevices[index].MonikerString);
                videoSource.NewFrame += VideoSource_NewFrame;
                videoSource.Start();

                lblStatus.Text = "Статус: Камера подключена";
                lblStatus.ForeColor = Color.DarkGreen;

                btnConnect.Text = "Отключить камеру";
                //btnScanOrder.Enabled = true;
                //btnScanLabel.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при подключении камеры: " + ex.Message);
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (isClosing) return;

            try
            {
                Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
                if (videoPreview.InvokeRequired)
                {
                    videoPreview.Invoke(new Action(() =>
                    {
                        videoPreview.Image?.Dispose();
                        videoPreview.Image = frame;
                    }));
                }
                else
                {
                    videoPreview.Image?.Dispose();
                    videoPreview.Image = frame;
                }
            }
            catch
            {
                // игнорируем возможные ошибки при отрисовке кадра
            }
        }

        //private void BtnScanOrder_Click(object sender, EventArgs e)
        //{
        //    if (string.IsNullOrEmpty(currentOrderNumber))
        //    {
        //        MessageBox.Show("Номер заказа не задан. Отсканируйте штрих-код или введите номер.",
        //                        "Ошибка",
        //                        MessageBoxButtons.OK,
        //                        MessageBoxIcon.Error);
        //        txtOrderNumber.Focus();
        //        return;
        //    }

        //    // Здесь должна быть твоя логика начала фиксации упаковки:
        //    // создание папки заказа, сохранение START кадра и т.п.
        //    // Пока просто показываем сообщение:
        //    MessageBox.Show($"Начата фиксация упаковки для заказа: {currentOrderNumber}",
        //                    "Инфо",
        //                    MessageBoxButtons.OK,
        //                    MessageBoxIcon.Information);
        //}

        //private void BtnScanLabel_Click(object sender, EventArgs e)
        //{
        //    if (string.IsNullOrEmpty(currentOrderNumber))
        //    {
        //        MessageBox.Show("Номер заказа не задан. Отсканируйте штрих-код или введите номер.",
        //                        "Ошибка",
        //                        MessageBoxButtons.OK,
        //                        MessageBoxIcon.Error);
        //        txtOrderNumber.Focus();
        //        return;
        //    }

        //    // Здесь твоя логика фиксации этикетки
        //    MessageBox.Show($"Фиксация этикетки для заказа: {currentOrderNumber}",
        //                    "Инфо",
        //                    MessageBoxButtons.OK,
        //                    MessageBoxIcon.Information);
        //}

        private void TxtOrderNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // не "пищать" и не добавлять символ
                StartOrderFromScanner();
            }
        }

        private void StartOrderFromScanner()
        {
            string orderNumber = txtOrderNumber.Text.Trim();

            if (string.IsNullOrEmpty(orderNumber))
            {
                MessageBox.Show("Номер заказа пустой. Отсканируйте штрих-код ещё раз.",
                                "Внимание",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return;
            }

            // Если сейчас НЕ идёт запись — начинаем новую
            if (!isRecording)
            {
                currentOrderNumber = orderNumber;

                // TODO: здесь мы чуть позже добавим СТАРТ записи видео
                // StartVideoRecording(currentOrderNumber);

                isRecording = true;
                recordingStartTime = DateTime.Now;
                lblTimer.Visible = true;
                lblTimer.Text = "00:00:00";
                timer.Start();

                lblStatus.Text = $"Статус: запись начата. Заказ: {currentOrderNumber}";
                lblStatus.ForeColor = Color.Red;

                return;
            }

            // Если запись уже идёт
            // Если отсканирован тот же код — останавливаем запись
            if (isRecording && orderNumber == currentOrderNumber)
            {
                // TODO: здесь чуть позже добавим ОСТАНОВКУ и сохранение видео
                // StopVideoRecording();

                isRecording = false;
                timer.Stop();
                lblTimer.Visible = false;

                lblStatus.Text = $"Статус: запись остановлена. Заказ: {currentOrderNumber}";
                lblStatus.ForeColor = Color.DarkGreen;

                // Можно очистить поле после завершения
                // txtOrderNumber.Text = "";
                // currentOrderNumber = "";

                return;
            }

            // Если запись идёт, но код ДРУГОЙ
            if (isRecording && orderNumber != currentOrderNumber)
            {
                MessageBox.Show($"Сейчас ведётся запись по заказу {currentOrderNumber}.\n" +
                                $"Сначала остановите запись, повторно отсканировав этот же штрих-код.",
                                "Запись уже идёт",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (isRecording)
            {
                TimeSpan elapsed = DateTime.Now - recordingStartTime;
                lblTimer.Text = elapsed.ToString(@"hh\:mm\:ss");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isClosing = true;

            if (videoSource != null && videoSource.IsRunning)
            {
                try
                {
                    videoSource.SignalToStop();
                    videoSource.NewFrame -= VideoSource_NewFrame;
                }
                catch { }
            }
        }
    }
}