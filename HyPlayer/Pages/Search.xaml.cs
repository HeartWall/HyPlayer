﻿using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using HyPlayer.Classes;
using HyPlayer.Controls;
using NeteaseCloudMusicApi;
using Newtonsoft.Json.Linq;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace HyPlayer.Pages
{
    /// <summary>
    ///     可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class Search : Page, IDisposable
    {
        private int page;
        private string Text = "";

        public Search()
        {
            InitializeComponent();
            NavigationViewSelector.SelectedItem = NavigationViewSelector.MenuItems[0];


            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var list = HistoryManagement.GetSearchHistory();
            foreach (var item in list)
            {
                var btn = new Button
                {
                    Content = item
                };
                btn.Click += Btn_Click;
                SearchHistory.Children.Add(btn);
            }

            if (e.Parameter != null)
            {
                Text = e.Parameter.ToString();
                LoadResult();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            SearchResultContainer.Children.Clear();
            GC.Collect();
        }

        private async void LoadResult()
        {
            if (string.IsNullOrEmpty(Text)) return;
            if (Convert.ToBase64String(Text.ToByteArrayUtf8()) == "6Ieq5p2A")
            {
                _ = Launcher.LaunchUriAsync(new Uri(@"http://music.163.com/m/topic/18926801"));
                return;
            }

            HistoryManagement.AddSearchHistory(Text);
            var list = HistoryManagement.GetSearchHistory();
            SearchHistory.Children.Clear();
            foreach (var item in list)
            {
                var btn = new Button
                {
                    Content = item
                };
                btn.Click += Btn_Click;
                SearchHistory.Children.Add(btn);
            }

            SearchResultContainer.Children.Clear();
            try
            {
                var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.Cloudsearch,
                    new Dictionary<string, object>
                    {
                        { "keywords", Text },
                        { "type", ((NavigationViewItem)NavigationViewSelector.SelectedItem).Tag.ToString() },
                        { "offset", page * 30 }
                    });

                switch (((NavigationViewItem)NavigationViewSelector.SelectedItem).Tag.ToString())
                {
                    case "1":
                        LoadSongResult(json);
                        break;
                    case "10":
                        LoadAlbumResult(json);
                        break;
                    case "100":
                        LoadArtistResult(json);
                        break;
                    case "1000":
                        LoadPlaylistResult(json);
                        break;
                    case "1009":
                        LoadRadioResult(json);
                        break;
                }
            }
            catch (Exception ex)
            {
                Common.ShowTeachingTip("发生错误", ex.Message);
            }
        }

        private void LoadRadioResult(JObject json)
        {
            foreach (var pljs in json["result"]["djRadios"].ToArray())
                SearchResultContainer.Children.Add(new SingleRadio(NCRadio.CreateFromJson(pljs)));
            if (int.Parse(json["result"]["djRadiosCount"].ToString()) >= (page + 1) * 30)
                NextPage.Visibility = Visibility.Visible;
            else
                NextPage.Visibility = Visibility.Collapsed;
            if (page > 0)
                PrevPage.Visibility = Visibility.Visible;
            else
                PrevPage.Visibility = Visibility.Collapsed;
        }

        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            Text = (sender as Button).Content.ToString();
            LoadResult();
        }

        private void LoadPlaylistResult(JObject json)
        {
            foreach (var pljs in json["result"]["playlists"].ToArray())
                SearchResultContainer.Children.Add(new SinglePlaylistStack(NCPlayList.CreateFromJson(pljs)));
            if (int.Parse(json["result"]["playlistCount"].ToString()) >= (page + 1) * 30)
                NextPage.Visibility = Visibility.Visible;
            else
                NextPage.Visibility = Visibility.Collapsed;
            if (page > 0)
                PrevPage.Visibility = Visibility.Visible;
            else
                PrevPage.Visibility = Visibility.Collapsed;
        }

        private void LoadArtistResult(JObject json)
        {
            foreach (var singerjson in json["result"]["artists"].ToArray())
                SearchResultContainer.Children.Add(new SingleArtist(NCArtist.CreateFromJson(singerjson)));
            if (int.Parse(json["result"]["artistCount"].ToString()) >= (page + 1) * 30)
                NextPage.Visibility = Visibility.Visible;
            else
                NextPage.Visibility = Visibility.Collapsed;
            if (page > 0)
                PrevPage.Visibility = Visibility.Visible;
            else
                PrevPage.Visibility = Visibility.Collapsed;
        }

        private void LoadAlbumResult(JObject json)
        {
            foreach (var albumjson in json["result"]["albums"].ToArray())
                SearchResultContainer.Children.Add(new SingleAlbum(NCAlbum.CreateFromJson(albumjson),
                    albumjson["artists"].ToArray().Select(t => NCArtist.CreateFromJson(t)).ToList()));
            if (int.Parse(json["result"]["albumCount"].ToString()) >= (page + 1) * 30)
                NextPage.Visibility = Visibility.Visible;
            else
                NextPage.Visibility = Visibility.Collapsed;
            if (page > 0)
                PrevPage.Visibility = Visibility.Visible;
            else
                PrevPage.Visibility = Visibility.Collapsed;
        }

        private void LoadSongResult(JObject json)
        {
            var idx = 0;
            try
            {
                foreach (var song in json["result"]["songs"].ToArray())
                {
                    var ncSong = NCSong.CreateFromJson(song);
                    SearchResultContainer.Children.Add(new SingleNCSong(ncSong, idx++,
                        song["privilege"]["st"].ToString() == "0"));
                }

                if (int.Parse(json["result"]["songCount"].ToString()) >= (page + 1) * 30)
                    NextPage.Visibility = Visibility.Visible;
                else
                    NextPage.Visibility = Visibility.Collapsed;
                if (page > 0)
                    PrevPage.Visibility = Visibility.Visible;
                else
                    PrevPage.Visibility = Visibility.Collapsed;
            }
            catch
            {
                Common.ShowTeachingTip("出现错误", json["msg"].ToString());
            }
        }


        private void PrevPage_OnClick(object sender, RoutedEventArgs e)
        {
            page--;
            LoadResult();
        }

        private void NextPage_OnClickPage_OnClick(object sender, RoutedEventArgs e)
        {
            page++;
            LoadResult();
        }

        private void NavigationView_OnSelectionChanged(NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            page = 0;
            LoadResult();
        }

        public void Dispose()
        {
            SearchResultContainer.Children.Clear();
        }

        private async void SearchKeywordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace((sender as AutoSuggestBox)?.Text))
            {
                try
                {
                    var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SearchHot);

                    ((AutoSuggestBox)sender).ItemsSource =
                        json["result"]["hots"].ToArray().ToList().Select(t => t["first"].ToString());
                }
                catch (Exception ex)
                {
                    Common.ShowTeachingTip("发生错误", ex.Message);
                }
            }
        }

        private void SearchKeywordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ((AutoSuggestBox)sender).ItemsSource = null;
        }

        private void SearchKeywordBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            Text = sender.Text;
            LoadResult();
        }

        private void SearchKeywordBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString();
        }

        private async void SearchKeywordBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(sender.Text))
            {
                AutoSuggestBox_GotFocus(sender, null);
                return;
            }

            try
            {
                var json = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SearchSuggest,
                    new Dictionary<string, object> { { "keywords", sender.Text }, { "type", "mobile" } });

                if (json["result"] != null && json["result"]["allMatch"] != null &&
                    json["result"]["allMatch"].HasValues)
                    sender.ItemsSource = json["result"]["allMatch"].ToArray().ToList()
                        .Select(t => t["keyword"].ToString())
                        .ToList();
            }
            catch (Exception ex)
            {
                Common.ShowTeachingTip("发生错误", ex.Message);
            }
        }
    }
}