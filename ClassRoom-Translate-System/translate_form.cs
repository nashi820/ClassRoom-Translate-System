
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Google.Cloud.Vision.V1;
using NAudio.Wave;
using NAudio.Codecs;
using NAudio.CoreAudioApi;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using Python.Runtime;


namespace WindowsFormsApp1
{
    public partial class translate_form : Form
    {
        int WIDTH = 640;
        int HEIGHT = 480;
        System.Drawing.Point MD = new System.Drawing.Point();
        System.Drawing.Point MU = new System.Drawing.Point();
        bool view = false;
        Mat frame; //描画用
        Mat img;
        Mat frame_movie; //録画用
        VideoCapture capture;
        Bitmap bmp;
        Bitmap bmp_init;
        Graphics graphic;

        int sqr_width = 0;  // square 幅
        int sqr_height = 0; // square 高さ

        static string text;
        static string voice_text;
        static string text_result;
        static string voice_text_result;

        //録画
        VideoWriter video;

        //録音
        WaveInEvent waveIn;
        WaveFileWriter waveWriter;

        //音声翻訳用
        WaveInEvent waveIn2;
        WaveFileWriter waveWriter2;


        public translate_form()
        {
            InitializeComponent();

            Console.Write("translate_form\n");

            // アクセストークンの取得
            Test.api_token();

            // ウインドウのサイズを固定
            this.FormBorderStyle = FormBorderStyle.FixedSingle;


            if (main_form.flag == 0)
            {
                Console.WriteLine("flag = 0: (New Lesson)");
            }
            else
            {
                Console.WriteLine("flag = 1: View");
            }

            //カメラ画像取得用のVideoCapture作成
            capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                MessageBox.Show("camera was not found!");
            }
            capture.FrameWidth = WIDTH;
            capture.FrameHeight = HEIGHT;

            //取得先のMat作成
            frame = new Mat(HEIGHT, WIDTH, MatType.CV_8UC3);

            //録画のためのMat作成
            frame_movie = new Mat(HEIGHT, WIDTH, MatType.CV_8UC3);

            //矩形用のMat作成
            //img = new Mat(HEIGHT, WIDTH, MatType.CV_8UC3);

            //表示用のBitmap作成
            bmp = new Bitmap(frame.Cols, frame.Rows, (int)frame.Step(), System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Data);

            //録画のためにvideoを作成
            //video = new VideoWriter(@"C:\cs_source\CL_source\2020-11-24\video.avi", FourCC.MJPG, 60, new OpenCvSharp.Size(frame.Cols, frame.Rows));
            video = new VideoWriter(main_form.video_path + "video.avi", FourCC.MJPG, 60, new OpenCvSharp.Size(frame_movie.Cols, frame_movie.Rows));
            
            //PictureBoxを出力サイズに合わせる
            pictureBox1.Width = frame.Cols;
            pictureBox1.Height = frame.Rows;

            //録音
            var deviceNumber = 0;
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = deviceNumber;
            waveIn.WaveFormat = new WaveFormat(44100, 16, 1);

            waveWriter = new WaveFileWriter(main_form.video_path + "audio.wav", waveIn.WaveFormat);

            waveIn.DataAvailable += (_, ee) =>
            {
                waveWriter.Write(ee.Buffer, 0, ee.BytesRecorded);
                waveWriter.Flush();
            };
            waveIn.RecordingStopped += (_, __) =>
            {
                //waveWriter.Flush();
            };

            waveIn.StartRecording();


            //描画用のGraphics作成
            graphic = pictureBox1.CreateGraphics();


            //画像取得スレッド開始
            backgroundWorker1.RunWorkerAsync();


            //録画スレッド開始
            backgroundWorker2.RunWorkerAsync();


        }

        //woker1 = キャプチャの表示用のwoker
        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

            //描画
            graphic.DrawImage(bmp, 0, 0, frame.Cols, frame.Rows);

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Console.Write("backgroundWorker1_DoWorker\n");

            BackgroundWorker bw = (BackgroundWorker)sender;

            while (!backgroundWorker1.CancellationPending)
            {
                //画像取得
                capture.Grab();
                NativeMethods.videoio_VideoCapture_operatorRightShift_Mat(capture.CvPtr, frame.CvPtr);

                bw.ReportProgress(0);
            }
        }

        // woker2 = 録画用のwoker
        private void backgroundWorker2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //録画処理
            video.Write(frame_movie); //Writeの引数はbitmapではなくInputArray(Mat)
        }


        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            Console.Write("backgroundWorker2_DoWorker\n");

            BackgroundWorker bw2 = (BackgroundWorker)sender;

            while (!backgroundWorker2.CancellationPending)
            {
                //画像取得
                capture.Grab();
                NativeMethods.videoio_VideoCapture_operatorRightShift_Mat(capture.CvPtr, frame_movie.CvPtr);

                bw2.ReportProgress(0);
            }

        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Console.Write("Form1_FormClosing\n");

            //スレッドの終了を待機
            backgroundWorker1.CancelAsync();
            while (backgroundWorker1.IsBusy)
                Application.DoEvents();

            //スレッド2の終了を待機
            backgroundWorker2.CancelAsync();
            while (backgroundWorker2.IsBusy)
                Application.DoEvents();

            //録画の終了処理
            video.Dispose();

            //録音の終了
            waveIn.StopRecording();
            waveIn.Dispose();
            waveIn = null;

            waveWriter.Close();
            waveWriter = null;

            // 音声ファイルを無音ごとに分割する
            // 初期化 (明示的に呼ばなくても内部で自動実行されるようだが、一応呼ぶ)
            PythonEngine.Initialize();

            dynamic vad = Py.Import("my_awesome_lib.VAD");
            vad.vad(main_form.video_path + "audio.wav");
            Console.WriteLine(main_form.video_path + "audio.wav");
            
            //確かめ
            dynamic mymath = Py.Import("my_awesome_lib.my_math");
            mymath.helloworld();

            // python環境を破棄
            PythonEngine.Shutdown();



        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Console.Write("pictureBox1_Click\n");

            /*
            Graphics g = Graphics.FromImage(bmp);

            if (backgroundWorker1_cancel_flag == false)
            {
                backgroundWorker1.CancelAsync();
                backgroundWorker1_cancel_flag = true;
                //保存されたキャプチャ画像の出力
                //Cv2.ImShow("test1", frame);

            }
            else
            {
                backgroundWorker1.RunWorkerAsync();
                backgroundWorker1_cancel_flag = false;
            }
            
            frame.SaveImage(@"C:\cs_source\img\cap.png");
            using (Mat cap = new Mat(@"C:\cs_source\img\cap.png"))
            {
                //保存されたキャプチャ画像の出力
                Cv2.ImShow("test1", frame);
            }
            
            */
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            Console.Write("pictureBox1_MouseDown\n");

            if (view == false)
            {
                backgroundWorker1.CancelAsync();
                bmp_init = bmp;
                img = BitmapConverter.ToMat(bmp_init);

                //描画フラグON
                view = true;

            }

            // Mouseを押した座標を記録
            MD.X = e.X;
            MD.Y = e.Y;


        }

        async private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            Console.Write("pictureBox1_MouseUp\n");

            System.Drawing.Point start = new System.Drawing.Point();
            System.Drawing.Point end = new System.Drawing.Point();

            // Mouseを離した座標を記録
            MU.X = e.X;
            MU.Y = e.Y;

            // 画像のトリミング
            //GetRegion(startX, startY, endX, endY);
            // 座標から(X,Y)座標を計算
            GetRegion(MD, MU, ref start, ref end);
            sqr_width = GetLength(start.X, end.X);
            sqr_height = GetLength(start.Y, end.Y);

            //Cv2.ImShow("test1", frame.Clone(new Rect(start.X, start.Y, sqr_width, sqr_height)));
            if ((sqr_width > 0) && (sqr_height > 0))
            {
                Console.WriteLine("startX:" + start.X.ToString() + ",sqr_width:" + sqr_width.ToString() + ",startY:" + start.Y.ToString() + ",sqr_height:" + sqr_height.ToString());
                frame.Clone(new Rect(start.X, start.Y, sqr_width, sqr_height)).SaveImage(@"C:\cs_source\img\cap.png");

                // 文字の認識
                string filePath = @"C:\cs_source\img\cap.png";
                text = RunOCR(filePath);
                Console.WriteLine("text:" + text);

                //文字の翻訳
                await Test.recognize_api();

                //テキストボックスに文字を追加する
                if (text_result != "" || text != "")
                {
                    richTextBox1.AppendText(text + "\n");
                    richTextBox1.AppendText(text_result + "\n");
                    richTextBox1.AppendText("---------------------------------------------------\n");
                }

                text = "";
                text_result = "";

            }
            /* 確かめ
            using (Mat cap = new Mat(@"C:\cs_source\img\cap.png"))
            {
                //保存されたキャプチャ画像の出力
                Cv2.ImShow("test1", cap);
            }
            */

            if (view == true)
            {
                backgroundWorker1.RunWorkerAsync();
                view = false;

            }

        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            Console.Write("pictureBox1_MouseMove\n");

            System.Drawing.Point p = new System.Drawing.Point();
            System.Drawing.Point start = new System.Drawing.Point();
            System.Drawing.Point end = new System.Drawing.Point();

            // 描画フラグcheck
            if (view == false) return;

            p.X = e.X;
            p.Y = e.Y;

            // 座標から(X,Y)座標を計算
            GetRegion(MD, p, ref start, ref end);
            sqr_width = GetLength(start.X, end.X);
            sqr_height = GetLength(start.Y, end.Y);


            // ドラッグした領域を矩形で囲む
            Cv2.Rectangle(img, new Rect(start.X, start.Y, sqr_width, sqr_height), new Scalar(0, 0, 255), 3);
            bmp = BitmapConverter.ToBitmap(img);
            graphic.DrawImage(bmp, 0, 0, img.Cols, img.Rows);
            bmp.Dispose();
            img.Dispose();
            bmp = new Bitmap(frame.Cols, frame.Rows, (int)frame.Step(), System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Data);
            img = BitmapConverter.ToMat(bmp_init);

        }

        private void GetRegion(System.Drawing.Point p1, System.Drawing.Point p2, ref System.Drawing.Point start, ref System.Drawing.Point end)
        {
            start.X = Math.Min(p1.X, p2.X);
            start.Y = Math.Min(p1.Y, p2.Y);

            end.X = Math.Max(p1.X, p2.X);
            end.Y = Math.Max(p1.Y, p2.Y);
        }

        private int GetLength(int start, int end)
        {
            return Math.Abs(start - end);
        }


        private string RunOCR(string filePath)
        {
            //string filePath = @"C:\cs_source\img\cap.png";
            string ocr_text = "";

            // Load an image from a local file.
            var image = Google.Cloud.Vision.V1.Image.FromFile(filePath);
            var client = ImageAnnotatorClient.Create();
            var response = client.DetectDocumentText(image);

            if (response != null)
            {
                foreach (var page in response.Pages)
                {
                    //Console.WriteLine(string.Join("\n", page));

                    foreach (var block in page.Blocks)
                    {
                        foreach (var paragraph in block.Paragraphs)
                        {
                            foreach (var word in paragraph.Words)
                            {
                                foreach (var symbol in word.Symbols)
                                {
                                    ocr_text = ocr_text + symbol.Text;
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Text::" + ocr_text + "\n");

            return ocr_text;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Console.Write("Form1_Load");

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            //テキストボックスに文字を追加する
            richTextBox1.AppendText("テスト\n");
            richTextBox1.AppendText("---------------------------------------------------\n");
            //カレット位置を末尾に移動
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            //テキストボックスにフォーカスを移動
            richTextBox1.Focus();
            //カレット位置までスクロール
            richTextBox1.ScrollToCaret();

        }

        private void button2_Click(object sender, EventArgs e)
        {

            Console.WriteLine("button2_click(Form close)");

            //スレッドの終了を待機
            backgroundWorker1.CancelAsync();
            while (backgroundWorker1.IsBusy)
                Application.DoEvents();

            //スレッド2の終了を待機
            backgroundWorker2.CancelAsync();
            while (backgroundWorker2.IsBusy)
                Application.DoEvents();

            //録画の終了処理
            video.Dispose();

            //録音の終了
            waveIn.StopRecording();
            waveIn.Dispose();
            waveIn = null;

            waveWriter?.Close();
            waveWriter = null;

            // 音声ファイルを無音ごとに分割する
            // 初期化 (明示的に呼ばなくても内部で自動実行されるようだが、一応呼ぶ)
            PythonEngine.Initialize();

            dynamic vad = Py.Import("my_awesome_lib.VAD");
            vad.vad(main_form.video_path + "audio.wav");
            Console.WriteLine(main_form.video_path + "audio.wav");

            //確かめ
            dynamic mymath = Py.Import("my_awesome_lib.my_math");
            mymath.helloworld();

            // python環境を破棄
            PythonEngine.Shutdown();



            //画面を閉じる
            this.Close();
        }

        //現在利用可能なデバイスの表示
        private void button3_Click(object sender, EventArgs e)
        {
            //Console.WriteLine(GetDevices());
            var list = new List<string>();
            list = GetDevices();
            foreach (string device in list)
            {
                Console.WriteLine(device);
            }
        }

        //出力デバイス一覧を取得
        public List<string> GetDevices()
        {
            List<string> deviceList = new List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                deviceList.Add(capabilities.ProductName);
            }
            return deviceList;
        }

        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {

        }

        public static class Test
        {
            //static async Task API_Main(string[] args)
            public static async Task api_token()
            {
                //アクセストークン取得
                await ApiController.obtainAccessToken();
                Console.WriteLine("アクセストークンを取得します。");
                Console.WriteLine("アクセストークン=" + ApiController.accessToken);

            }

            //static async Task API_Main(string[] args)
            public static async Task recognize_api()
            {

                // 機械翻訳API呼び出し          
                //string text = "你好";
                //string text = "おはよう";
                //string text = "おはようございます";

                string source_lang = "ja";
                string target_lang = "en";
                Console.WriteLine("機械翻訳APIを呼び出します。");
                await ApiController.machineTranslation(text, source_lang, target_lang);
                Console.WriteLine(text + " の翻訳結果：" + ApiController.result);
                text_result = ApiController.result;

            }

            //static async Task API_Main(string[] args)
            public static async Task voice_translate_api()
            {

                // 機械翻訳API呼び出し          
                //string text = "你好";
                //string text = "おはよう";
                //string text = "おはようございます";

                string source_lang = "ja";
                string target_lang = "en";
                Console.WriteLine("機械翻訳APIを呼び出します。");
                await ApiController.machineTranslation(voice_text, source_lang, target_lang);
                Console.WriteLine(text + " の翻訳結果：" + ApiController.result);
                voice_text_result = ApiController.result; //音声を翻訳した結果が入る


            }

            //static async Task API_Main(string[] args)
            public static async Task voice_api()
            {

                // 音声認識API呼び出し
                string path_to_audio = @"C:\cs_source\img\onephrase.wav";
                Console.WriteLine("音声認識APIを呼び出します。");
                await ApiController.speechRecognition(path_to_audio);
                Console.WriteLine(path_to_audio + " ファイルの翻訳結果：\n" + ApiController.result);
                voice_text = ApiController.result; //音声を認識した結果が入る
            }

            //static async Task API_Main(string[] args)
            public static async Task both_voice_translate_api()
            {

                // 音声認識API呼び出し
                string path_to_audio = @"C:\cs_source\img\onephrase.wav";
                Console.WriteLine("音声認識APIを呼び出します。");
                await ApiController.speechRecognition(path_to_audio);
                Console.WriteLine(path_to_audio + " ファイルの翻訳結果：\n" + ApiController.result);
                voice_text = ApiController.result; //音声を認識した結果が入る

                string source_lang = "ja";
                string target_lang = "en";
                Console.WriteLine("機械翻訳APIを呼び出します。");
                await ApiController.machineTranslation(voice_text, source_lang, target_lang);
                Console.WriteLine(text + " の翻訳結果：" + ApiController.result);
                //Console.WriteLine(text + " の翻訳結果：" + ApiController.detail_result);
                voice_text_result = ApiController.result; //音声を翻訳した結果が入る
            }

        }

        public static class ApiController
        {
            // HTTP クライアント
            private static readonly HttpClient httpClient;
            // アクセストークン
            public static string accessToken = "";
            // 翻訳結果格納変数
            public static string result = "";
            //public static string detail_result = "";
            // アクセストークン取得用のパラメータ
            private static string token_uri = "https://auth.mimi.fd.ai/v2/token";
            private static string client_id = "*******";
            private static string client_secret = "*******";
            private static string scope = "https://apis.mimi.fd.ai/auth/nict-tts/http-api-service;https://apis.mimi.fd.ai/auth/nict-tra/http-api-service;https://apis.mimi.fd.ai/auth/nict-asr/http-api-service;https://apis.mimi.fd.ai/auth/nict-asr/websocket-api-service;https://apis.mimi.fd.ai/auth/applications.r";
            private static string grant_type = "https://auth.mimi.fd.ai/grant_type/application_credentials";
            // 音声認識API
            private static string speech_recognition_uri = "https://service.mimi.fd.ai";
            // 機械翻訳API
            private static string machine_translation_uri = "https://sandbox-mt.mimi.fd.ai/machine_translation";

            static ApiController()
            {
                // クライアントの初期化は一回のみ（通信毎に初期化しない）
                httpClient = new HttpClient();
            }

            public static async Task obtainAccessToken()
            {
                // POSTリクエストのパラメータ
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", client_id },
                {"client_secret", client_secret },
                {"scope", scope },
                {"grant_type", grant_type }
            });

                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(token_uri, content);
                    response.EnsureSuccessStatusCode();

                    // レスポンスを受け取る
                    string responseBody = await response.Content.ReadAsStringAsync();
                    // レスポンス（文字列で表現された JSON ）をデシリアライズ
                    AuthAPI authAPI = JsonSerializer.Deserialize<AuthAPI>(responseBody);
                    accessToken = authAPI.accessToken;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\n 例外を検出しました。");
                    Console.WriteLine("エラーメッセージ :{0} ", e.Message);
                }

            }

            public static async Task machineTranslation(string text, string source_lang, string target_lang)
            {
                // POSTリクエストのパラメータ
                var parameter = new Dictionary<string, string>
            {
                {"text", text },
                {"source_lang", source_lang },
                {"target_lang", target_lang }
            };
                var content = new FormUrlEncodedContent(parameter);
                var uri = new Uri(machine_translation_uri);
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Content = content;

                try
                {
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    // レスポンスを受け取る
                    string responseBody = await response.Content.ReadAsStringAsync();
                    string stringUnicode = responseBody.Replace("[\"", " ").Replace("\"]", " ").Trim();
                    Console.WriteLine("レスポンス=" + stringUnicode);

                    // Unicode文字列を日本語の文字列に変換する
                    byte[] bytesUnicode = Encoding.Unicode.GetBytes(stringUnicode);
                    result = Encoding.Unicode.GetString(bytesUnicode);
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\n 例外を検出しました。");
                    Console.WriteLine("エラーメッセージ :{0} ", e.Message);
                }

            }
            public static async Task speechRecognition(string path_to_audio)
            {
                // ファイル読み込み
                byte[] binary = File.ReadAllBytes(path_to_audio);
                // POSTリクエスト
                var uri = new Uri(speech_recognition_uri);
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                var content = new ByteArrayContent(binary);

                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Headers.Add("x-mimi-process", "nict-asr");
                request.Headers.Add("x-mimi-input-language", "ja");
                request.Content = content;
                request.Content.Headers.Add("Content-Type", "audio/x-pcm;bit=16;rate=44100;channels=1");
                request.Content.Headers.Add("Content-Length", binary.Length.ToString());

                Console.WriteLine(request.ToString());

                try
                {
                    Console.WriteLine("POSTリクエストを送信します。");
                    HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("レスポンスを受信します。");

                    // レスポンスを受け取る
                    string responseBody = await response.Content.ReadAsStringAsync();
                    result = responseBody;
                    //detail_result = responseBody;
                    MimiASR mimiASR = JsonSerializer.Deserialize<MimiASR>(responseBody);

                    result = "";
                    for (int i = 0; i < mimiASR.response.Length; i++)
                    {
                        result += mimiASR.response[i].result.Split('|')[0];
                        //detail_result += i.ToString() + ":" + mimiASR.response[i].result + "\n";
                    }
                    result = result + "\n";
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\n 例外を検出しました。");
                    Console.WriteLine("エラーメッセージ :{0} ", e.Message);
                }

            }
        }

        public class AuthAPI
        {
            public int startTimestamp { set; get; }
            public string selfLink { set; get; }
            public string kind { set; get; }
            public int code { set; get; }
            public string status { set; get; }
            public int expires_in { set; get; }
            public string targetLink { set; get; }
            public string operationId { set; get; }
            public string error { set; get; }
            public string accessToken { set; get; }
            public int endTimestamp { set; get; }
            public int progress { set; get; }
        }

        public class MimiASR
        {
            public Response[] response { set; get; }
            public class Response
            {
                public string result { set; get; }
            }
            public string session_id { set; get; }
            public string status { set; get; }
            public string type { set; get; }
        }

        async private void button4_Click(object sender, EventArgs e)
        {

            // 音声認識用
            int deviceNumber = 0;

            while (true)
            {
                //録音
                waveIn2 = new WaveInEvent();
                waveIn2.DeviceNumber = deviceNumber;
                waveIn2.WaveFormat = new WaveFormat(44100, 16, 1);

                waveWriter2 = new WaveFileWriter(@"C:\cs_source\img\onephrase.wav", waveIn2.WaveFormat);

                waveIn2.DataAvailable += (_, ee) =>
                {
                    waveWriter2.Write(ee.Buffer, 0, ee.BytesRecorded);
                    waveWriter2.Flush();
                };
                waveIn2.RecordingStopped += (_, __) =>
                {
                    //waveWriter2.Flush();
                };

                waveIn2.StartRecording();

                //using (Py.GIL())
                //{
                //    dynamic rec = Py.Import("my_awesome_lib.record2sec"); // "from my_awesome_lib import my_math"
                //    rec.vad(10); // クラスのインスタンスを生成
                //}

                Console.WriteLine("start delay");
                //await Task.Delay(3000); //3s
                await Task.Delay(5000);
                Console.WriteLine("end delay");
                waveIn2.StopRecording();
                waveIn2.Dispose();
                waveIn2 = null;

                waveWriter2.Close();
                waveWriter2 = null;

                await Test.both_voice_translate_api();

                //テキストボックスに文字を追加する
                if (voice_text_result != "")
                {
                    richTextBox2.AppendText(voice_text);
                    richTextBox2.AppendText(voice_text_result + "\n");
                    richTextBox2.AppendText("-------------------------\n");
                }

                voice_text = "";
                voice_text_result = "";


            }
        }
    }
}