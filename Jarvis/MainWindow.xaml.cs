using System;
using System.Windows;

//using System.Speech.Recognition;
using Microsoft.Speech.Recognition;
using Microsoft.Speech.Synthesis;

using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Jarvis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //SpeechRecognitionEngine recEngine = new SpeechRecognitionEngine();
        SpeechRecognitionEngine recEngine = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("ru-RU"));
        SpeechSynthesizer synth = new SpeechSynthesizer();

        static string jarvisName = "Елена";
        static double threshold = 0.6;
        static string recognize_language = "ru-RU";
        static string speech_language = "ru-RU, Elena";

        static string[] daysofweek;
        static string x_hours, x_minutes, recognized, ignored;

        static bool isAwake = true;

        static Choices commands = new Choices();
        static GrammarBuilder gBuilder;
        Grammar grammar;

        static Random rnd = new Random();

        static List<Command> commandsList = new List<Command>();

        public MainWindow()
        {
            InitializeComponent();

            //SistemVolumChanger.SetVolume(-1);

            LoadSettings("config_ru.txt");
        }

        private void RecEngine_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            //RecognizedText_TextBox.Text += String.Format("Предположение: \"{0}\"", e.Result.Text) + Environment.NewLine;
        }


        private void RecEngine_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            var comand = e.Result.Text;

            var confidence = e.Result.Confidence;

            if (confidence < threshold) return;

            Command cmd;

            try
            {
                var allCmds = commandsList.Where(c => c.VoiceCommand == comand).ToArray();
                cmd = allCmds[rnd.Next(allCmds.Count())];
            }
            catch (Exception ex)
            {
                RecognizedText_TextBox.Text += String.Format($"ОШИБКА: \"{e.Result.Text}\"") + Environment.NewLine;

                RecognizedText_TextBox.CaretIndex = RecognizedText_TextBox.Text.Length;
                RecognizedText_TextBox.ScrollToEnd();

                return;
            }

            if (cmd.Type == "on")
            {
                Say(cmd.Option);
                isAwake = true;
            }

            if (!isAwake)
            {
                RecognizedText_TextBox.Text += String.Format($"{ignored}: \"{e.Result.Text}\"") + Environment.NewLine;

                RecognizedText_TextBox.CaretIndex = RecognizedText_TextBox.Text.Length;
                RecognizedText_TextBox.ScrollToEnd();

                return;
            }


            RecognizedText_TextBox.Text += String.Format($"{recognized}: \"{e.Result.Text}\"") + Environment.NewLine;


            RecognizedText_TextBox.CaretIndex = RecognizedText_TextBox.Text.Length;
            RecognizedText_TextBox.ScrollToEnd();

            switch(cmd.Type)
            {
                case "off":
                    {
                        isAwake = false;
                        Say(cmd.Option);
                        break;
                    }


                case "switch_config":
                    {
                        LoadSettings(cmd.Option);
                        break;
                    }

                case "time":
                    {
                        SayTime();
                        break;
                    }

                case "day_of_week":
                    {
                        SayDayOfWeek();
                        break;
                    }

                case "day_number":
                    {
                        SayDayNumber();
                        break;
                    }

                case "open":
                    {
                        RunProcess(cmd.Option);
                        break;
                    }

                case "kill":
                    {
                        CloseProgram(cmd.Option);
                        break;
                    }

                case "say":
                    {
                        Say(cmd.Option);
                        break;
                    }

                case "die":
                    {
                        Say(cmd.Option, true);
                        this.Close();
                        break;
                    }

                case "set_volume":
                    {
                        SistemVolumChanger.SetVolume(int.Parse(cmd.Option));

                        break;
                    }
            }
        }


        void Say(string textToSay, [Optional] bool isDelay)
        {
            if(isDelay)
                synth.Speak(textToSay);
            else
                synth.SpeakAsync(textToSay);
        }

        void SayTime()
        {
            var timeText = DateTime.Now.ToString($"HH") + x_hours + DateTime.Now.ToString($"mm") + x_minutes;

            Say(timeText);
        }


        void SayDayOfWeek()
        {
            var dateToSpeak = daysofweek[(int)DateTime.Now.DayOfWeek - 1];

            Say(dateToSpeak);
        }

        void SayDayNumber()
        {
            var dayNumber = DateTime.Now.ToString("dd");

            Say(dayNumber);
        }

        void RunProcess(string path)
        {
            try
            {
                Process.Start(path);
            }
            catch
            {

            }            
        }


        void CloseProgram(string executableName)
        {
            foreach(var process in Process.GetProcessesByName(executableName))
            {
                try
                {
                    process.Kill();
                }
                catch
                {

                }
            }
        }


        void LoadSettings(string configFileName)
        {
            var settings = File.ReadAllText(configFileName);

            jarvisName = Regex.Match(settings, "name: \"(.+?)\"").Groups[1].Value;
            threshold = double.Parse(Regex.Match(settings, "confidence_threshold: (.+?) ", RegexOptions.Compiled).Groups[1].Value.Replace('.', ','));
            recognize_language = Regex.Match(settings, "recognize_language: \"(.+?)\"").Groups[1].Value;
            speech_language = Regex.Match(settings, "speech_language: \"(.+?)\"").Groups[1].Value;
            #region DaysOfWeek
            var daysMatch = Regex.Match(settings, "daysofweek:((\\s\"(?<DayName>.+?)\")+)");
            var daysCaptures = daysMatch.Groups["DayName"].Captures;

            var daysList = new List<string>();

            foreach(Capture match in daysCaptures)
            {
                daysList.Add(match.Value);
            }

            daysofweek = daysList.ToArray();
            #endregion

            x_hours = Regex.Match(settings, "x_hours: \"(.+?)\"").Groups[1].Value;
            x_minutes = Regex.Match(settings, "x_minutes: \"(.+?)\"").Groups[1].Value;
            recognized = Regex.Match(settings, "recognized: \"(.+?)\"").Groups[1].Value;
            ignored = Regex.Match(settings, "ignored: \"(.+?)\"").Groups[1].Value;

            commandsList.Clear();

            var commandsMatches = Regex.Matches(settings, "^\"(?<VoiceCommand>.+?)\"\\s*\"(?<CommandType>.+?)\"(?: \"(?<Option>.+?)\")*", RegexOptions.Multiline);

            foreach(Match m in commandsMatches)
            {
                var cmd = new Command();

                cmd.VoiceCommand = m.Groups["VoiceCommand"].Value;
                cmd.Type = m.Groups["CommandType"].Value;
                cmd.Option = m.Groups["Option"].Value;

                commandsList.Add(cmd);
            }

            var allVoiceCommands = commandsList.Select(c => c.VoiceCommand).ToArray();

            commands = new Choices();
            commands.Add(allVoiceCommands);

            gBuilder = new GrammarBuilder();
            gBuilder.Append(commands);
            gBuilder.Culture = new System.Globalization.CultureInfo(recognize_language);

            recEngine = new SpeechRecognitionEngine(new System.Globalization.CultureInfo(recognize_language));

            grammar = new Grammar(gBuilder);

            recEngine.LoadGrammarAsync(grammar);
            recEngine.SetInputToDefaultAudioDevice();
            recEngine.SpeechRecognized += RecEngine_SpeechRecognized;
            recEngine.SpeechHypothesized += RecEngine_SpeechHypothesized;
            recEngine.RecognizeAsync(RecognizeMode.Multiple);

            

            var voice = new SpeechSynthesizer().GetInstalledVoices().Where(v => v.VoiceInfo.Name.Contains(speech_language)).ToArray()[0].VoiceInfo.Name;

            synth.SelectVoice(voice);
            synth.SetOutputToDefaultAudioDevice();
            synth.Volume = 100;            
        }


        class Command
        {
            public string VoiceCommand { get; set; }
            public string Type { get; set; }
            public string Option { get; set; }
        }
    }
}
