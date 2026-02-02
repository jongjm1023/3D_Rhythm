using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class SongListController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    
    private void Start()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) 
        {
            Debug.LogError("SongListController: No UIDocument found!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        
        // Play Menu BGM by default when entering song list
        if (SongManager.Instance != null) SongManager.Instance.PlayMenuMusic();

        // Panels
        var scrollView = root.Q<ScrollView>("SongList");
        var playButton = root.Q<Button>("PlayButton");
        
        // Detail Elements
        var detailTitle = root.Q<Label>("DetailTitle");
        var detailArtist = root.Q<Label>("DetailArtist");
        var detailBPM = root.Q<Label>("DetailBPM");
        var detailLevel = root.Q<Label>("DetailLevel");
        var detailBestScore = root.Q<Label>("DetailBestScore");
        var detailMaxCombo = root.Q<Label>("DetailMaxCombo");
        
        var albumCover = root.Q<VisualElement>("AlbumCover");

        if (playButton != null)
        {
            playButton.clicked += () => 
            {
                Debug.Log("Play Button Clicked"); // Debugging
                SongManager.Instance.PlayGame();
            };
        }

        // ... existing ScrollView logic ...

        foreach (var song in SongManager.Instance.songLibrary)
        {
            Debug.Log($"SongListController: Adding button for {song.title}");
            
            // Create item container
            var itemContainer = new Button();
            itemContainer.AddToClassList("song-item");
            
            // Title Label
            var titleLabel = new Label(song.title);
            titleLabel.AddToClassList("song-title");
            itemContainer.Add(titleLabel);

            // Artist Label
            var artistLabel = new Label(song.artist);
            artistLabel.AddToClassList("song-artist");
            itemContainer.Add(artistLabel);

            // Click Event
            itemContainer.clicked += () => 
            {
                // Update Logic: Select Song but DO NOT load scene immediately
                SongManager.Instance.SelectSong(song);
                
                // Play Preview
                if (song.musicInfo != null)
                {
                    SongManager.Instance.PlayPreview(song.musicInfo);
                }

                // Update Detail Panel
                if (detailTitle != null) detailTitle.text = song.title;
                if (detailArtist != null) detailArtist.text = song.artist;
                if (detailBPM != null) detailBPM.text = $"BPM: {song.bpm}";
                if (detailLevel != null) detailLevel.text = $"Lv. {song.level}";
                
                if (detailBestScore != null) detailBestScore.text = $"Best Score: {song.maxScore}";
                if (detailMaxCombo != null) detailMaxCombo.text = $"Max Combo: {song.maxCombo}";

                if (albumCover != null && song.albumCover != null) 
                {
                    albumCover.style.backgroundImage = new StyleBackground(song.albumCover);
                }
                else if (albumCover != null)
                {
                    albumCover.style.backgroundImage = null; // Clear if null
                }
            };
            
            scrollView.Add(itemContainer);
        }
        
        // Select first song by default if available?
        if (SongManager.Instance.songLibrary.Count > 0)
        {
            // Trigger click on first item? Or just manual update
            // Ideally simulate selection
        }
    }
}
