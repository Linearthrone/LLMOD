using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class VirtualEnvironmentControlsWindowViewModel : ObservableObject
    {
        private readonly IVirtualEnvironmentService? _virtualEnvironmentService;

        // Scene Information
        private string _sceneName = "No scene";
        public string SceneName
        {
            get => _sceneName;
            set => SetProperty(ref _sceneName, value);
        }

        private string _sceneDetails = "Not connected";
        public string SceneDetails
        {
            get => _sceneDetails;
            set => SetProperty(ref _sceneDetails, value);
        }

        // Avatars
        public ObservableCollection<AvatarViewModel> Avatars { get; } = new();

        // Selected Avatar
        private string? _selectedAvatarId;
        public bool HasSelectedAvatar => !string.IsNullOrEmpty(_selectedAvatarId);
        public string SelectedAvatarName => GetSelectedAvatar()?.Name ?? "No avatar selected";
        public string SelectedAvatarId => _selectedAvatarId ?? "";

        // Position Controls
        private string _positionX = "0";
        public string PositionX
        {
            get => _positionX;
            set => SetProperty(ref _positionX, value);
        }

        private string _positionY = "0";
        public string PositionY
        {
            get => _positionY;
            set => SetProperty(ref _positionY, value);
        }

        private string _positionZ = "0";
        public string PositionZ
        {
            get => _positionZ;
            set => SetProperty(ref _positionZ, value);
        }

        private string _rotationY = "0";
        public string RotationY
        {
            get => _rotationY;
            set => SetProperty(ref _rotationY, value);
        }

        private string _animationName = "";
        public string AnimationName
        {
            get => _animationName;
            set => SetProperty(ref _animationName, value);
        }

        // Commands
        public ICommand SpawnAvatarCommand { get; }
        public ICommand SetPositionCommand { get; }
        public ICommand SetRotationCommand { get; }
        public ICommand MoveAvatarCommand { get; }
        public ICommand AnimateAvatarCommand { get; }
        public ICommand UpdatePoseCommand { get; }

        public VirtualEnvironmentControlsWindowViewModel(IVirtualEnvironmentService? virtualEnvironmentService)
        {
            _virtualEnvironmentService = virtualEnvironmentService;

            SpawnAvatarCommand = new RelayCommand(async () => await SpawnAvatarAsync());
            SetPositionCommand = new RelayCommand(() => { });
            SetRotationCommand = new RelayCommand(() => { });
            MoveAvatarCommand = new RelayCommand(async () => await MoveAvatarAsync());
            AnimateAvatarCommand = new RelayCommand(async () => await AnimateAvatarAsync());
            UpdatePoseCommand = new RelayCommand(async () => await UpdatePoseAsync());

            if (_virtualEnvironmentService != null)
            {
                _virtualEnvironmentService.StatusChanged += OnStatusChanged;
                _virtualEnvironmentService.SceneUpdated += OnSceneUpdated;
            }

            // Load initial scene information
            _ = LoadSceneInfoAsync();
        }

        private void OnStatusChanged(object? sender, VirtualEnvironmentEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SceneName = e.Status.CurrentScene ?? "No scene";
                SceneDetails = e.Status.IsConnected
                    ? $"Avatars: {e.Status.AvatarCount} | FPS: {e.Status.FrameRate:F1} | Rendering: {(e.Status.IsRendering ? "Yes" : "No")}"
                    : "Not connected";
            });
        }

        private void OnSceneUpdated(object? sender, SceneUpdateEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.UpdateType == "AvatarSpawned" && e.Data is AvatarDefinition avatar)
                {
                    Avatars.Add(new AvatarViewModel
                    {
                        Id = avatar.Id,
                        Name = avatar.Name,
                        Details = $"Position: ({avatar.Position.X:F1}, {avatar.Position.Y:F1}, {avatar.Position.Z:F1})"
                    });
                }
            });
        }

        private async Task LoadSceneInfoAsync()
        {
            if (_virtualEnvironmentService == null) return;

            try
            {
                var status = await _virtualEnvironmentService.GetStatusAsync();
                SceneName = status.CurrentScene ?? "No scene";
                SceneDetails = status.IsConnected
                    ? $"Avatars: {status.AvatarCount} | FPS: {status.FrameRate:F1} | Rendering: {(status.IsRendering ? "Yes" : "No")}"
                    : "Not connected";

                // Try to get scene information
                if (status.IsConnected)
                {
                    try
                    {
                        var sceneInfo = await _virtualEnvironmentService.GetSceneInformationAsync();
                        // Parse scene info if needed
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading scene info: {ex.Message}");
            }
        }

        public void SelectAvatar(string avatarId)
        {
            _selectedAvatarId = avatarId;
            OnPropertyChanged(nameof(HasSelectedAvatar));
            OnPropertyChanged(nameof(SelectedAvatarName));
            OnPropertyChanged(nameof(SelectedAvatarId));

            // Load avatar state
            _ = LoadAvatarStateAsync(avatarId);
        }

        private async Task LoadAvatarStateAsync(string avatarId)
        {
            if (_virtualEnvironmentService == null) return;

            try
            {
                var state = await _virtualEnvironmentService.GetAvatarStateAsync(avatarId);
                if (state.TryGetValue("Position", out var posObj) && posObj is Dictionary<string, object> pos)
                {
                    if (pos.TryGetValue("X", out var x)) PositionX = x.ToString() ?? "0";
                    if (pos.TryGetValue("Y", out var y)) PositionY = y.ToString() ?? "0";
                    if (pos.TryGetValue("Z", out var z)) PositionZ = z.ToString() ?? "0";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading avatar state: {ex.Message}");
            }
        }

        private AvatarViewModel? GetSelectedAvatar()
        {
            return Avatars.FirstOrDefault(a => a.Id == _selectedAvatarId);
        }

        private async Task SpawnAvatarAsync()
        {
            if (_virtualEnvironmentService == null)
            {
                MessageBox.Show("Virtual Environment service is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Avatar Model",
                    Filter = "Model Files|*.fbx;*.obj;*.gltf;*.glb|All Files|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var avatar = new AvatarDefinition
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                        ModelPath = dialog.FileName,
                        Position = new Vector3(0, 0, 0)
                    };

                    var result = await _virtualEnvironmentService.SpawnAvatarAsync(avatar);
                    MessageBox.Show($"Avatar spawned: {result}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    Avatars.Add(new AvatarViewModel
                    {
                        Id = avatar.Id,
                        Name = avatar.Name,
                        Details = $"Model: {System.IO.Path.GetFileName(avatar.ModelPath)}"
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error spawning avatar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task MoveAvatarAsync()
        {
            if (_virtualEnvironmentService == null || string.IsNullOrEmpty(_selectedAvatarId))
            {
                MessageBox.Show("Please select an avatar first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (float.TryParse(PositionX, out var x) &&
                    float.TryParse(PositionY, out var y) &&
                    float.TryParse(PositionZ, out var z) &&
                    float.TryParse(RotationY, out var rotY))
                {
                    var result = await _virtualEnvironmentService.MoveAvatarAsync(_selectedAvatarId, x, y, z, rotY);
                    MessageBox.Show($"Avatar moved: {result}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Invalid position or rotation values.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving avatar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AnimateAvatarAsync()
        {
            if (_virtualEnvironmentService == null || string.IsNullOrEmpty(_selectedAvatarId))
            {
                MessageBox.Show("Please select an avatar first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(AnimationName))
            {
                MessageBox.Show("Please enter an animation name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = await _virtualEnvironmentService.AnimateAvatarAsync(_selectedAvatarId, AnimationName);
                MessageBox.Show($"Animation started: {result}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error animating avatar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdatePoseAsync()
        {
            if (_virtualEnvironmentService == null || string.IsNullOrEmpty(_selectedAvatarId))
            {
                MessageBox.Show("Please select an avatar first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (float.TryParse(PositionX, out var x) &&
                    float.TryParse(PositionY, out var y) &&
                    float.TryParse(PositionZ, out var z))
                {
                    var pose = new Pose
                    {
                        Position = new Vector3(x, y, z),
                        Rotation = new Vector3(0, float.TryParse(RotationY, out var rotY) ? rotY : 0, 0)
                    };

                    var result = await _virtualEnvironmentService.UpdateAvatarPoseAsync(_selectedAvatarId, pose);
                    MessageBox.Show($"Pose updated: {result}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Invalid position values.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating pose: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class AvatarViewModel : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
