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
        private int testOrderCounter = 1;
        private bool isClosing = false;

        private ComboBox cmbCameras;
        private Button btnConnect;
        private PictureBox videoPreview;
        private Button btnScanOrder;
        private Button btnScanLabel;
        private Label lblStatus;
        private Label lblTimer;
        private System.Windows.Forms.Timer timer;

        public Form1()
        {
            InitializeComponent();
            SetupForm();
        }

        private void SetupForm()
        {
            this.Text = "ТЕСТ: Видеофиксация упаковки";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Создаем папку для записей
            string recordingsPath = Path.Combine(Application.StartupPath, "TestRecordings");
            if (!Directory.Exists(recordingsPath))
                Directory.CreateDirectory(recordingsPath);

            InitializeComponents();
            FindCameras();
        }

        private void InitializeComponents()
        {
            // ComboBox для выбора камеры
            cmbCameras = new ComboBox();
            cmbCameras.Location = new Point(10, 10);
            cmbCameras.Size = new Size(300, 21);
            this.Controls.Add(cmbCameras);

            // Кнопка подключения камеры
            btnConnect = new Button();
            btnConnect.Location = new Point(320, 10);
            btnConnect.Size = new Size(100, 23);
            btnConnect.Text = "Подключить камеру";
            btnConnect.Click += BtnConnect_Click;
            this.Controls.Add(btnConnect);

            // Окно для просмотра видео с камеры
            videoPreview = new PictureBox();
            videoPreview.Location = new Point(10, 40);
            videoPreview.Size = new Size(640, 480);
            videoPreview.BorderStyle = BorderStyle.FixedSingle;
            videoPreview.BackColor = Color.Black;
            this.Controls.Add(videoPreview);

            // Кнопка "Сканировать заказ" (имитация сканера)
            btnScanOrder = new Button();
            btnScanOrder.Location = new Point(10, 530);
            btnScanOrder.Size = new Size(150, 30);
            btnScanOrder.Text = "СКАНИРОВАТЬ ЗАКАЗ";
            btnScanOrder.BackColor = Color.LightGreen;
            btnScanOrder.Enabled = false;
            btnScanOrder.Click += BtnScanOrder_Click;
            this.Controls.Add(btnScanOrder);

            // Кнопка "Сканировать этикетку" (имитация сканера)
            btnScanLabel = new Button();
            btnScanLabel.Location = new Point(170, 530);
            btnScanLabel.Size = new Size(150, 30);
            btnScanLabel.Text = "СКАНИРОВАТЬ ЭТИКЕТКУ";
            btnScanLabel.BackColor = Color.LightCoral;
            btnScanLabel.Enabled = false;
            btnScanLabel.Click += BtnScanLabel_Click;
            this.Controls.Add(btnScanLabel);

            // Надпись статуса
            lblStatus = new Label();
            lblStatus.Location = new Point(330, 535);
            lblStatus.Size = new Size(300, 20);
            lblStatus.Text = "Статус: Отключено";
            lblStatus.ForeColor = Color.Red;
            this.Controls.Add(lblStatus);

            // Таймер записи
            lblTimer = new Label();
            lblTimer.Location = new Point(660, 15);
            lblTimer.Size = new Size(100, 20);
            lblTimer.Text = "00:00:00";
            lblTimer.Font = new Font("Arial", 10, FontStyle.Bold);
            lblTimer.ForeColor = Color.Red;
            lblTimer.Visible = false;
            this.Controls.Add(lblTimer);

            // Таймер для обновления времени записи
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
        }

        private void FindCameras()
        {
            try
            {
                // Ищем все подключенные камеры
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (videoDevices.Count == 0)
                {
                    MessageBox.Show("Камеры не найдены! Проверьте подключение камеры.");
                    return;
                }

                // Добавляем камеры в список
                foreach (FilterInfo device in videoDevices)
                {
                    cmbCameras.Items.Add(device.Name);
                }
                cmbCameras.SelectedIndex = 0;

                lblStatus.Text = $"Найдено камер: {videoDevices.Count}";
                lblStatus.ForeColor = Color.Blue;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска камер: {ex.Message}");
            }
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                // Отключаем предыдущую камеру
                DisconnectCamera();

                if (cmbCameras.SelectedIndex >= 0)
                {
                    // Подключаем выбранную камеру
                    videoSource = new VideoCaptureDevice(videoDevices[cmbCameras.SelectedIndex].MonikerString);
                    videoSource.NewFrame += VideoSource_NewFrame;
                    videoSource.Start();

                    lblStatus.Text = "Камера подключена ✓";
                    lblStatus.ForeColor = Color.Green;
                    btnScanOrder.Enabled = true;

                    MessageBox.Show("Камера успешно подключена! Теперь можно тестировать запись упаковки.",
                                  "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения камеры: {ex.Message}",
                              "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // Не обрабатываем кадры если программа закрывается
            if (isClosing) return;

            try
            {
                // Показываем видео с камеры
                if (videoPreview.InvokeRequired)
                {
                    if (!isClosing)
                    {
                        videoPreview.Invoke(new Action<Bitmap>((bitmap) =>
                        {
                            if (!isClosing)
                                videoPreview.Image = bitmap;
                        }), (Bitmap)eventArgs.Frame.Clone());
                    }
                }
                else
                {
                    if (!isClosing)
                        videoPreview.Image = (Bitmap)eventArgs.Frame.Clone();
                }
            }
            catch
            {
                // Игнорируем ошибки отображения
            }
        }

        private void BtnScanOrder_Click(object sender, EventArgs e)
        {
            // Имитация сканирования заказа - НАЧАТЬ ЗАПИСЬ
            currentOrderNumber = $"TEST_ORDER_{testOrderCounter:000}";
            testOrderCounter++;

            isRecording = true;
            recordingStartTime = DateTime.Now;

            // Сохраняем скриншот начала упаковки
            SaveScreenshot("START");

            // Меняем интерфейс
            btnScanOrder.Enabled = false;
            btnScanLabel.Enabled = true;
            lblStatus.Text = $"🔴 ЗАПИСЬ: {currentOrderNumber}";
            lblStatus.ForeColor = Color.Red;
            lblTimer.Visible = true;
            timer.Start();

            MessageBox.Show($"Запись начата!\nНомер заказа: {currentOrderNumber}\n\nТеперь упакуйте товар перед камерой и нажмите 'СКАНИРОВАТЬ ЭТИКЕТКУ' когда закончите.",
                          "Запись начата", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnScanLabel_Click(object sender, EventArgs e)
        {
            // Имитация сканирования этикетки - ЗАКОНЧИТЬ ЗАПИСЬ
            if (!isRecording) return;

            isRecording = false;
            timer.Stop();

            // Сохраняем скриншот конца упаковки
            SaveScreenshot("END");
            SaveRecordingInfo();

            // Восстанавливаем интерфейс
            btnScanOrder.Enabled = true;
            btnScanLabel.Enabled = false;
            lblStatus.Text = "Запись завершена ✓";
            lblStatus.ForeColor = Color.Green;
            lblTimer.Visible = false;

            string recordingTime = (DateTime.Now - recordingStartTime).ToString(@"hh\:mm\:ss");

            MessageBox.Show($"Запись завершена!\nЗаказ: {currentOrderNumber}\nДлительность: {recordingTime}\n\nФайлы сохранены в папке:\nTestRecordings\\{currentOrderNumber}",
                          "Запись завершена", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveScreenshot(string type)
        {
            try
            {
                if (videoPreview.Image != null && !isClosing)
                {
                    // Создаем папку для этого заказа
                    string folderPath = Path.Combine(Application.StartupPath, "TestRecordings", currentOrderNumber);
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    // Сохраняем скриншот
                    string fileName = Path.Combine(folderPath, $"{type}_{DateTime.Now:HHmmss}.jpg");
                    videoPreview.Image.Save(fileName, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
            }
            catch
            {
                // Игнорируем ошибки сохранения
            }
        }

        private void SaveRecordingInfo()
        {
            try
            {
                string folderPath = Path.Combine(Application.StartupPath, "TestRecordings", currentOrderNumber);
                string infoFile = Path.Combine(folderPath, "!_INFO.txt");

                string info = $@"ИНФОРМАЦИЯ О ЗАПИСИ УПАКОВКИ

Номер заказа: {currentOrderNumber}
Начало записи: {recordingStartTime}
Конец записи: {DateTime.Now}
Длительность: {DateTime.Now - recordingStartTime}

Сохраненные файлы:
- START_*.jpg - скриншот начала упаковки
- END_*.jpg - скриншот конца упаковки

Эта запись была создана тестовой программой видеофиксации упаковки.";

                File.WriteAllText(infoFile, info);
            }
            catch
            {
                // Игнорируем ошибки сохранения информации
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (isRecording && !isClosing)
            {
                // Обновляем таймер
                TimeSpan duration = DateTime.Now - recordingStartTime;
                lblTimer.Text = duration.ToString(@"hh\:mm\:ss");
            }
        }

        private void DisconnectCamera()
        {
            if (videoSource != null)
            {
                try
                {
                    // Отключаем обработчик видео
                    videoSource.NewFrame -= VideoSource_NewFrame;

                    // Останавливаем камеру
                    if (videoSource.IsRunning)
                    {
                        videoSource.SignalToStop();
                    }
                }
                catch
                {
                    // Игнорируем ошибки отключения
                }
                finally
                {
                    videoSource = null;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Устанавливаем флаг что программа закрывается
            isClosing = true;

            // Останавливаем таймер
            timer?.Stop();

            // Отключаем камеру
            DisconnectCamera();

            base.OnFormClosing(e);
        }
    }
}