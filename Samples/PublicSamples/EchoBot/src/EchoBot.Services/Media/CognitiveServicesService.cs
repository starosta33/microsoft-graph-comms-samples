using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Skype.Bots.Media;
using EchoBot.Services.ServiceSetup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using CognitiveServices.Translator;
using CognitiveServices.Translator.Translate;

using Microsoft.Extensions.Logging;

namespace EchoBot.Services.Media
{
    /// <summary>
    /// Class CognitiveServicesService.
    /// </summary>
    public class CognitiveServicesService
    {
        /// <summary>
        /// The is the indicator if the media stream is running
        /// </summary>
        private bool _isRunning = false;

        /// <summary>
        /// The is draining indicator
        /// </summary>
        protected bool _isDraining;

        private readonly AppSettings settings;
        private readonly ITranslateClient translateClient;

        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;
        private readonly PushAudioInputStream _audioInputStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
        private readonly AudioOutputStream _audioOutputStream = AudioOutputStream.CreatePullStream();

        private readonly SpeechConfig _speechConfig;
        private readonly SpeechSynthesizer _synthesizer;
        private SpeechRecognizer _recognizer;
        private string currentSpeakerDisplayName;

        /// <summary>
        /// Initializes a new instance of the <see cref="CognitiveServicesService" /> class.
        public CognitiveServicesService(
            AppSettings settings,
            ITranslateClient translateClient,
            ILogger logger)
        {
            this.settings = settings;
            this.translateClient = translateClient ?? throw new ArgumentNullException(nameof(translateClient));
            _logger = logger;

            _speechConfig = SpeechConfig.FromSubscription(settings.SpeechConfigKey, settings.SpeechConfigRegion);
            _speechConfig.SpeechRecognitionLanguage = settings.SpeechRecognitionLanguage;
            _speechConfig.SpeechSynthesisLanguage = settings.SpeechSynthesisLanguage;

            var audioConfig = AudioConfig.FromStreamOutput(_audioOutputStream);
            _synthesizer = new SpeechSynthesizer(_speechConfig, audioConfig);
        }

        /// <summary>
        /// Appends the audio buffer.
        /// </summary>
        /// <param name="audioBuffer"></param>
        public async Task AppendAudioBuffer(AudioMediaBuffer audioBuffer)
        {
            if (!_isRunning)
            {
                Start();
                await ProcessSpeech();
            }

            try
            {
                // audio for a 1:1 call (or unmixed audio)
                var bufferLength = audioBuffer.Length;
                if (bufferLength > 0)
                {
                    var buffer = new byte[bufferLength];
                    Marshal.Copy(audioBuffer.Data, buffer, 0, (int)bufferLength);

                    _audioInputStream.Write(buffer);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception happened writing to input stream");
            }
        }

        public async Task AppendAudioBuffer(UnmixedAudioBuffer audioBuffer, string displayName)
        {
            // TODO must be running in parallel and per speaker??!!
            this.currentSpeakerDisplayName = displayName;
            this._logger.LogDebug($"Unmixed audio buffer received for speaker id: {audioBuffer.ActiveSpeakerId}, name: {displayName}");

            if (!_isRunning)
            {
                Start();
                await ProcessSpeech();
            }

            try
            {
                var bufferLength = audioBuffer.Length;
                if (bufferLength > 0)
                {
                    var buffer = new byte[bufferLength];
                    Marshal.Copy(audioBuffer.Data, buffer, 0, (int)bufferLength);

                    _audioInputStream.Write(buffer);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception happened writing to input stream");
            }
        }

        protected virtual void OnSendMediaBufferEventArgs(object sender, MediaStreamEventArgs e)
        {
            this.SendMediaBuffer?.Invoke(this, e);
        }

        public event EventHandler<MediaStreamEventArgs> SendMediaBuffer;

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ShutDownAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_isRunning)
            {
                await _recognizer.StopContinuousRecognitionAsync();
                _recognizer.Dispose();
                _audioInputStream.Dispose();
                _audioOutputStream.Dispose();
                _synthesizer.Dispose();

                _isRunning = false;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        private void Start()
        {
            if (!_isRunning)
            {
                _isRunning = true;
            }
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        private async Task ProcessSpeech()
        {
            try
            {
                var stopRecognition = new TaskCompletionSource<int>();

                using (var audioInput = AudioConfig.FromStreamInput(_audioInputStream))
                {
                    if (_recognizer == null)
                    {
                        _logger.LogInformation("init recognizer");
                        _recognizer = new SpeechRecognizer(_speechConfig, audioInput);
                    }
                }

                _recognizer.Recognizing += (s, e) =>
                {
                    _logger.LogDebug($"RECOGNIZING: Text={e.Result.Text}");
                };

                _recognizer.Recognized += async (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        if (string.IsNullOrEmpty(e.Result.Text))
                            return;

                        _logger.LogInformation($"===> From={this.currentSpeakerDisplayName}, Text={e.Result.Text}");

                        // We recognized the speech
                        // Now do Speech to Text
                        if (this.settings.UseTextToSpeech)
                        {
                            await TextToSpeech(e.Result.Text);
                        }
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        _logger.LogInformation($"NOMATCH: Speech could not be recognized.");
                    }
                };

                _recognizer.Canceled += (s, e) =>
                {
                    _logger.LogInformation($"CANCELED: Reason={e.Reason}");

                    if (e.Reason == CancellationReason.Error)
                    {
                        _logger.LogInformation($"CANCELED: ErrorCode={e.ErrorCode}");
                        _logger.LogInformation($"CANCELED: ErrorDetails={e.ErrorDetails}");
                        _logger.LogInformation($"CANCELED: Did you update the subscription info?");
                    }

                    stopRecognition.TrySetResult(0);
                };

                _recognizer.SessionStarted += async (s, e) =>
                {
                    _logger.LogInformation("\nSession started event.");
                    await TextToSpeech("Welcome to the call.", translate: false);
                };

                _recognizer.SessionStopped += (s, e) =>
                {
                    _logger.LogInformation("\nSession stopped event.");
                    _logger.LogInformation("\nStop recognition.");
                    stopRecognition.TrySetResult(0);
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Waits for completion.
                // Use Task.WaitAny to keep the task rooted.
                Task.WaitAny(new[] { stopRecognition.Task });

                // Stops recognition.
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "The queue processing task object has been disposed.");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions and log
                _logger.LogError(ex, $"Caught Exception: {ex.Message}");
            }

            _isDraining = false;
        }

        private async Task TextToSpeech(string text, bool translate = true)
        {
            if (translate && _speechConfig.SpeechRecognitionLanguage != _speechConfig.SpeechSynthesisLanguage)
            {
                var translatedText = Translate(text, _speechConfig.SpeechRecognitionLanguage, _speechConfig.SpeechSynthesisLanguage);
                text = translatedText.First().Translations.First().Text;
            }

            // convert the text to speech
            SpeechSynthesisResult result = await _synthesizer.SpeakTextAsync(text);
            // take the stream of the result
            // create 20ms media buffers of the stream
            // and send to the AudioSocket in the BotMediaStream
            using var stream = AudioDataStream.FromResult(result);

            var currentTick = DateTime.Now.Ticks;
            MediaStreamEventArgs args = new MediaStreamEventArgs
            {
                AudioMediaBuffers = Util.Utilities.CreateAudioMediaBuffers(stream, currentTick, this._logger)
            };
            this.OnSendMediaBufferEventArgs(this, args);
        }

        private IList<ResponseBody> Translate(string text, string fromLanguage, string toLanguage) {
            var response = translateClient.Translate(
                new RequestContent(text),
                new RequestParameter
                {
                    From = fromLanguage, // Optional, will be auto-discovered
                    To = new[] { toLanguage }, // You can translate to multiple language at once.
                    IncludeAlignment = true, // Return what was translated by what. (see documentation)
                });

            // response = array of sentences + array of target language
            _logger.LogInformation(response.First().Translations.First().Text);

            return response;
        }
    }
}
