using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
#if MELONLOADER
using MelonLoader;
#endif
#if BEPINEX
using BepInEx;
#endif
using UnityEngine;
using UnityEngine.UI;
using RDLevelEditor;
using System.Reflection;
using System.Threading.Tasks;
using RDPlaySongVortex.ArrowVortex;
using RDTools;

namespace RDPlaySongVortex
{
#if MELONLOADER
    public class RhythmVortex : MelonMod
    {
        public override void OnApplicationStart()
        {
            var harmony = new HarmonyLib.Harmony("com.balancedlight.rdplaysongvortex");
            harmony.PatchAll();
            LoggerInstance.Msg("Arrow Vortex BPM finder LOADED!!");
        }
    }
#endif

#if BEPINEX
    [BepInPlugin("com.balancedlight.rdplaysongvortex", "RDPlaySongVortex", "1.0.0")]
    public class RhythmVortexPlugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;
        private void Awake()
        {
            Log = Logger;
            var harmony = new HarmonyLib.Harmony("com.balancedlight.rdplaysongvortex");
            harmony.PatchAll();
            Logger.LogInfo("Arrow Vortex BPM finder LOADED!!");
        }
    }
#endif

    public static class ModLogger
    {
        public static void Msg(string msg)
        {
#if MELONLOADER
            MelonLogger.Msg(msg);
#endif
#if BEPINEX
            RhythmVortexPlugin.Log?.LogInfo(msg);
#endif
        }

        public static void Error(string msg)
        {
#if MELONLOADER
            MelonLogger.Error(msg);
#endif
#if BEPINEX
            RhythmVortexPlugin.Log?.LogError(msg);
#endif
        }
    }

    public class BPMCalculatorExtension : MonoBehaviour
    {
        public InputField durationInput;
        public GameObject durationGroup;
    }

    [HarmonyPatch(typeof(BPMCalculator), "Start")]
    public static class BPMCalculator_Start_Patch
    {
        public static void Postfix(BPMCalculator __instance)
        {
            if (__instance.GetComponent<BPMCalculatorExtension>() != null) return;

            var ext = __instance.gameObject.AddComponent<BPMCalculatorExtension>();

            // Adjust percentage text to allow overflow (so you can actually SEE the guesses)
            // Probably not the best way of doing all of this but it works*
            __instance.percentageText.verticalOverflow = VerticalWrapMode.Overflow;

            GameObject container = new GameObject("VortexSettings");
            container.transform.SetParent(__instance.calculateBPMButton.transform.parent, false);
            
            // Layout
            RectTransform rt = container.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200, 30);
            rt.anchoredPosition = new Vector2(0, -60); 

            // Try to find an existing input field to steal styles from
            InputField existingInput = __instance.GetComponentInParent<InspectorPanel>()?.GetComponentInChildren<InputField>(true);
            
            // Create input field 
            GameObject inputObj = new GameObject("VortexDurationInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObj.transform.SetParent(container.transform, false);

            RectTransform inputRT = inputObj.GetComponent<RectTransform>();
            inputRT.sizeDelta = new Vector2(60, 16);
            inputRT.anchoredPosition = new Vector2(50, 0);

            var inputBg = inputObj.GetComponent<Image>();
            InputField tmpInput = inputObj.GetComponent<InputField>();
            tmpInput.contentType = InputField.ContentType.DecimalNumber;

            // Text component
            GameObject textObj = new GameObject("VortexDurationText", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(inputObj.transform, false);
            var textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0);
            textRT.anchorMax = new Vector2(1, 1);
            textRT.offsetMin = new Vector2(6, 4);
            textRT.offsetMax = new Vector2(-6, -4);
            var text = textObj.GetComponent<Text>();
            
            // Default styles
            inputBg.color = Color.white; 
            text.fontSize = 8;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "10";
            
            // Steal styles from existing input if found
            if (existingInput != null)
            {
                if (existingInput.image != null)
                {
                    inputBg.sprite = existingInput.image.sprite;
                    inputBg.type = existingInput.image.type;
                }

                if (existingInput.textComponent != null)
                {
                    text.font = existingInput.textComponent.font;
                    text.color = existingInput.textComponent.color;
                }
            }
            else
            {
                 text.font = RDString.GetAppropiateFontForString(text.text);
            }

            tmpInput.textComponent = text;
            tmpInput.text = "10";

            // Label for calculation duration
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(container.transform, false);
            var label = labelObj.GetComponent<Text>();
            label.text = "Calculation Duration:";
            label.fontSize = 8;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleRight;
            
            if (text.font != null) label.font = text.font;
            else label.font = RDString.GetAppropiateFontForString(label.text);

            if (existingInput != null)
            {
                labelObj.AddComponent<Outline>().effectDistance = new Vector2(1, -1);
            }

            RectTransform labelRT = labelObj.GetComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(110, 22);
            labelRT.anchoredPosition = new Vector2(-50, 0);

            ext.durationInput = tmpInput;
            ext.durationGroup = container;
            
            ext.durationGroup.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(BPMCalculator), "Update")]
    public static class BPMCalculator_Update_Patch
    {
        public static void Postfix(BPMCalculator __instance)
        {
            var ext = __instance.GetComponent<BPMCalculatorExtension>();
            if (ext == null || ext.durationGroup == null) return;

            bool show = false;
            if (__instance.currentInspectorPanel != null && __instance.currentInspectorPanel.currentLevelEvent is LevelEvent_SetBeatsPerMinute)
            {
                show = true;
            }
            
            if (ext.durationGroup.activeSelf != show)
            {
                ext.durationGroup.SetActive(show);
            }
        }
    }

    [HarmonyPatch(typeof(BPMCalculator), "Initialize")]
    public static class BPMCalculator_Initialize_Patch
    {
        private static System.Threading.CancellationTokenSource _cts;

        public static bool Prefix(BPMCalculator __instance)
        {
            __instance.StartCoroutine(CalculateBPMRoutine(__instance));
            return false;
        }

        private static System.Collections.IEnumerator CalculateBPMRoutine(BPMCalculator instance)
        {
            var bpmButtonTextField = AccessTools.Field(typeof(BPMCalculator), "bpmButtonText");
            Text bpmButtonText = (Text)bpmButtonTextField.GetValue(instance);

            bpmButtonText.text = "Cancel";
            instance.percentageText.text = "Analyzing audio...\n(This may take a while for long audio clips)";
            instance.calculateBPMButton.interactable = true;

            _cts = new System.Threading.CancellationTokenSource();
            instance.calculateBPMButton.onClick.RemoveAllListeners();
            instance.calculateBPMButton.onClick.AddListener(() => 
            {
                _cts.Cancel();
                instance.percentageText.text = "Cancelling...";
                instance.calculateBPMButton.interactable = false;
            });

            // Get Audio and Time Range
            var soundData = instance.data;
            double startTime = 0;
            double duration = -1; // -1 means full length

            var currentEvent = instance.currentInspectorPanel?.currentLevelEvent;
            
            if (soundData == null && currentEvent is LevelEvent_PlaySong playSongEvent)
            {
                soundData = playSongEvent.song.ToSoundData(true);
            }

            // Handle Change BPM Event
            if (currentEvent is LevelEvent_SetBeatsPerMinute changeBPMEvent)
            {
                // Find previous PlaySong event
                var events = LevelEvent_Base.level?.data?.levelEvents;
                LevelEvent_PlaySong lastPlaySong = null;
                
                var sortedEvents = events?.OrderBy(e => e.bar).ThenBy(e => e.beat).ToList();
                if (sortedEvents == null)
                {
                    instance.percentageText.text = "<color=#d92433>Error: Cannot read level events!</color>";
                    ResetButton(instance, bpmButtonText);
                    yield break;
                }

                // Find the last PlaySong event that occurs before or at the same time as the current event
                lastPlaySong = sortedEvents
                    .Where(e => e is LevelEvent_PlaySong && 
                               (e.bar < changeBPMEvent.bar || (e.bar == changeBPMEvent.bar && e.beat <= changeBPMEvent.beat)))
                    .LastOrDefault() as LevelEvent_PlaySong;

                if (lastPlaySong != null)
                {
                    soundData = lastPlaySong.song.ToSoundData(true);
                    startTime = CalculateTimeBetweenEvents(lastPlaySong, changeBPMEvent, sortedEvents);
                    
                    // Get duration from UI
                    var ext = instance.GetComponent<BPMCalculatorExtension>();
                    if (ext != null && float.TryParse(ext.durationInput.text, out float d))
                    {
                        duration = d;
                    }
                }
                else
                {
                    instance.percentageText.text = "<color=#d92433>Error: No preceding Play Song event found!</color>";
                    ResetButton(instance, bpmButtonText);
                    yield break;
                }
            }

            if (soundData == null)
            {
                instance.percentageText.text = "<color=#d92433>Error: No Sound Data!</color>";
                ResetButton(instance, bpmButtonText);
                yield break;
            }

            // Check if audio is loaded
            string key = soundData.conductorFilename;
            if (!Singleton<AudioManager>.Instance.audioLib.ContainsKey(key))
            {
                // Some alternatives
                if (Singleton<AudioManager>.Instance.audioLib.ContainsKey(soundData.filename))
                {
                    key = soundData.filename;
                }
                else if (Singleton<AudioManager>.Instance.audioLib.ContainsKey(soundData.filename + "*external"))
                {
                    key = soundData.filename + "*external";
                }
                else
                {
                     ModLogger.Msg($"Could not find key '{key}' or variants. Available keys in AudioLib:");
                     foreach(var k in Singleton<AudioManager>.Instance.audioLib.Keys)
                     {
                         if (k.Contains(soundData.filename)) ModLogger.Msg($" - {k}");
                     }

                     instance.percentageText.text = "<color=#d92433>Error: Audio not loaded! (try pressing play first?)</color>";
                     ResetButton(instance, bpmButtonText);
                     yield break;
                }
            }

            AudioClip clip = Singleton<AudioManager>.Instance.audioLib[key];
            
            // Get samples (must be on main thread)
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            int sampleRate = clip.frequency;
            int channels = clip.channels;

            // Mix down to mono
            float[] monoSamples = new float[clip.samples];
            if (channels > 1)
            {
                for (int i = 0; i < clip.samples; i++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        sum += samples[i * channels + c];
                    }
                    monoSamples[i] = sum / channels;
                }
            }
            else
            {
                Array.Copy(samples, monoSamples, samples.Length);
            }

            // Slice audio 
            if (startTime > 0 || duration > 0)
            {
                int startSample = (int)(startTime * sampleRate);
                int lengthSamples = (duration > 0) ? (int)(duration * sampleRate) : (monoSamples.Length - startSample);
                
                double audioLengthSeconds = (double)monoSamples.Length / sampleRate;
                
                //ModLogger.Msg($"Audio Analysis Debug - Start Time: {startTime:F2}s, Audio Length: {audioLengthSeconds:F2}s, Duration: {duration:F2}s");
                
                if (startSample < 0) startSample = 0;
                if (startSample >= monoSamples.Length) 
                {
                     instance.percentageText.text = $"<color=#d92433>Error: Start time ({startTime:F1}s) is after\n audio end ({audioLengthSeconds:F1}s)!</color>";
                     ResetButton(instance, bpmButtonText);
                     yield break;
                }
                if (startSample + lengthSamples > monoSamples.Length) lengthSamples = monoSamples.Length - startSample;
                
                float[] slicedSamples = new float[lengthSamples];
                Array.Copy(monoSamples, startSample, slicedSamples, 0, lengthSamples);
                monoSamples = slicedSamples;
            }

            // Run in background
            var token = _cts.Token;
            var task = Task.Run(() => 
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    var onsets = OnsetDetector.Detect(monoSamples, sampleRate);
                    token.ThrowIfCancellationRequested();
                    return TempoDetector.CalculateBPM(onsets, monoSamples, sampleRate, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    ModLogger.Error("Something went wrong in the BPM calculation: " + e);
                    throw;
                }
            }, token);

            while (!task.IsCompleted) yield return null;

            if (task.IsCanceled)
            {
                instance.percentageText.text = "Cancelled.";
                ResetButton(instance, bpmButtonText);
                yield break;
            }

            if (task.IsFaulted)
            {
                instance.percentageText.text = "<color=#d92433>Error: " + task.Exception.InnerException.Message + "</color>";
                ResetButton(instance, bpmButtonText);
                yield break;
            }

            var results = task.Result;

            // show results
            if (results.Count > 0)
            {
                var best = results[0];
                instance.audioBPM = (float)best.bpm;
                
                double maxFitness = results.Max(r => r.fitness);
                
                string resultText = $"Result: {best.bpm:F2} BPM\n\nGuesses:\n";
                foreach(var r in results.Take(5))
                {
                    double percentage = (r.fitness / maxFitness) * 100.0;
                    resultText += $"{r.bpm:F2} (Conf: {percentage:F0}%)\n";
                }
                instance.percentageText.text = resultText;

                if (instance.onBeatsPerMinuteUpdated != null)
                {
                    instance.onBeatsPerMinuteUpdated(instance.audioBPM);
                }
            }
            else
            {
                instance.percentageText.text = "Could not detect BPM!";
            }

            ResetButton(instance, bpmButtonText);
        }

        private static void ResetButton(BPMCalculator instance, Text bpmButtonText)
        {
            bpmButtonText.text = "Calculate BPM";
            instance.calculateBPMButton.interactable = true;
            instance.calculateBPMButton.onClick.RemoveAllListeners();
            instance.calculateBPMButton.onClick.AddListener(instance.Initialize);
        }

        private static double CalculateTimeBetweenEvents(LevelEvent_PlaySong start, LevelEvent_SetBeatsPerMinute end, List<LevelEvent_Base> allEvents)
        {
            double totalTime = 0;
            
            // Get all timing events between start and end positions
            var timingEvents = allEvents.Where(e => 
                (e is LevelEvent_SetBeatsPerMinute || e is LevelEvent_SetCrotchetsPerBar) &&
                IsAfter(e, start) && IsBefore(e, end)
            ).ToList();
            
            // Start with the BPM from the PlaySong event
            float currentBPM = start.beatsPerMinute;
            int currentCPB = 8;
            
            // Check if there's a SetBeatsPerMinute at the exact same position as PlaySong (shouldn't happen, but people be crazy)
            var samePosNPM = allEvents.OfType<LevelEvent_SetBeatsPerMinute>()
                .FirstOrDefault(e => e.bar == start.bar && e.beat == start.beat);
            if (samePosNPM != null)
            {
                currentBPM = samePosNPM.beatsPerMinute;
            }
            
            // Find the most recent SetCrotchetsPerBar at or before the start position
            var preCPB = allEvents.OfType<LevelEvent_SetCrotchetsPerBar>()
                .Where(e => e.bar < start.bar || (e.bar == start.bar && e.beat <= start.beat))
                .OrderByDescending(e => e.bar).ThenByDescending(e => e.beat)
                .FirstOrDefault();
            if (preCPB != null) currentCPB = preCPB.crotchetsPerBar;

            var points = new List<TimingPoint>();
            points.Add(new TimingPoint { bar = start.bar, beat = start.beat, type = PointType.Start });
            
            foreach(var e in timingEvents)
            {
                points.Add(new TimingPoint { bar = e.bar, beat = e.beat, evt = e, type = PointType.Change });
            }
            
            points.Add(new TimingPoint { bar = end.bar, beat = end.beat, type = PointType.End });
            
            points = points.OrderBy(p => p.bar).ThenBy(p => p.beat).ToList();

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i+1];
                
                if (p1.evt is LevelEvent_SetBeatsPerMinute bpmEvt) currentBPM = bpmEvt.beatsPerMinute;
                if (p1.evt is LevelEvent_SetCrotchetsPerBar cpbEvt) currentCPB = cpbEvt.crotchetsPerBar;
                
                double beats = (p2.bar - p1.bar) * currentCPB + (p2.beat - p1.beat);
                double seconds = beats * (60.0 / currentBPM);
                
                ModLogger.Msg($"  Segment: Bar {p1.bar}.{p1.beat:F2} -> Bar {p2.bar}.{p2.beat:F2} | {beats:F2} beats @ {currentBPM} BPM (CPB:{currentCPB}) = {seconds:F2}s");
                totalTime += seconds;
            }
            
            ModLogger.Msg($"Total time from Bar {start.bar}.{start.beat:F2} to Bar {end.bar}.{end.beat:F2}: {totalTime:F2}s");
            return totalTime;
        }

        private static bool IsAfter(LevelEvent_Base a, LevelEvent_Base b)
        {
            if (a.bar > b.bar) return true;
            if (a.bar == b.bar && a.beat > b.beat) return true;
            return false;
        }

        private static bool IsBefore(LevelEvent_Base a, LevelEvent_Base b)
        {
            if (a.bar < b.bar) return true;
            if (a.bar == b.bar && a.beat < b.beat) return true;
            return false;
        }

        class TimingPoint
        {
            public int bar;
            public float beat;
            public LevelEvent_Base evt;
            public PointType type;
        }
        
        enum PointType { Start, Change, End }
    }
}
