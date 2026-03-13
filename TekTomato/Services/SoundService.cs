using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace TekTomato.Services
{
    /// <summary>
    /// Handles sound playback and available sound file enumeration.
    /// </summary>
    public class SoundService
    {
        #region Private Fields

        private readonly string _windowsMediaPath;
        private readonly SettingsService _settingsService;

        #endregion Private Fields

        #region Constructor

        /// <summary>
        /// Initialises the sound service with settings dependency.
        /// </summary>
        public SoundService(SettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            // Default Windows media path
            _windowsMediaPath = @"C:\Windows\Media\";
        }

        #endregion Constructor

        #region Public Methods

        /// <summary>
        /// Gets all available .wav files from the Windows Media directory.
        /// </summary>
        /// <returns>Enumeration of sound file names (filenames only, not paths).</returns>
        public IEnumerable<string> GetAvailableSounds()
        {
            var sounds = new List<string>();

            try
            {
                if (Directory.Exists(_windowsMediaPath))
                {
                    var wavFiles = Directory.GetFiles(_windowsMediaPath, "*.wav", SearchOption.TopDirectoryOnly);
                    
                    foreach (var file in wavFiles)
                    {
                        sounds.Add(Path.GetFileName(file));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Handle access denied gracefully - return empty list
                System.Diagnostics.Debug.WriteLine("Access denied to Windows Media folder");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating sounds: {ex.Message}");
            }

            return sounds;
        }

        /// <summary>
        /// Plays the currently selected sound file with fallback to system beep.
        /// </summary>
        public async Task PlaySelectedSound()
        {
            var fileName = _settingsService.SoundFileName;
            
            if (string.IsNullOrEmpty(fileName))
            {
                // No sound configured - play default system beep
                SystemSounds.Beep.Play();
                return;
            }

            // Reconstruct full path from filename only
            var fullPath = Path.Combine(_windowsMediaPath, fileName);

            try
            {
                if (File.Exists(fullPath))
                {
                    await PlaySoundFileAsync(fullPath);
                }
                else
                {
                    // File not found - fallback to system beep
                    SystemSounds.Beep.Play();
                }
            }
            catch (Exception ex)
            {
                // Error playing sound - fallback to system beep
                System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
                try
                {
                    SystemSounds.Beep.Play();
                }
                catch
                {
                    // Ignore final error - no sound will play
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Plays a WAV file asynchronously using SoundPlayer.
        /// Creates new instance per play to avoid issues with overlapping playback.
        /// </summary>
        private async Task PlaySoundFileAsync(string fullPath)
        {
            // Create new SoundPlayer instance for each play (as per architecture reference)
            var soundPlayer = new SoundPlayer(fullPath);

            // Use LoadAsync + Play on completion pattern
            await soundPlayer.LoadAsync();
            
            try
            {
                // Wait for the LoadCompleted event and then play
                await Task.Run(() => soundPlayer.Play());
                
                // Give it time to start before potentially disposing
                System.Threading.Thread.Sleep(100);
            }
            finally
            {
                // Dispose the sound player after use
                soundPlayer.Dispose();
            }
        }

        #endregion Private Methods
    }
}