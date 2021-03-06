﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Linq;
using System.Xml;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace NewsBuddy
{
    public class TopicWorker : BackgroundWorker
    {
        public APTopic assignedTopic { get; set; }
        public bool unfollowed = false;
    }
    /// <summary>
    /// Interaction logic for APReader.xaml
    /// </summary>
    public partial class APReader : Window
    {
        public APingestor ingest;

        private bool isLoaded = false;

        bool freezeFeed = false;

        private bool autoFeed { get; set; }

        private string[] updateMessages =
        {
            "Updating  Live",
            "Updating  Live",
            "Updating  Live",
            "Updating  Live"

        };

        private int updateMessageNumber = 0;

        public MainWindow mainWindow { get; set; }

        public ObservableCollection<APObject> listObjects = new ObservableCollection<APObject>();

        private readonly BackgroundWorker worker = new BackgroundWorker();

        private List<TopicWorker> topicWorkers = new List<TopicWorker>();

        public APReader(bool noOwner = false)
        {
            InitializeComponent();
            this.DataContext = this;
            if (noOwner)
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            list_APStories.ItemsSource = listObjects;

            lbl_EditorDisclaimer.Visibility = Visibility.Collapsed;
            btn_DeleteChecked.Visibility = Visibility.Collapsed;
            btn_DeleteUnChecked.Visibility = Visibility.Collapsed;

            worker.DoWork += WorkerFeedUpdate;
            worker.RunWorkerCompleted += WorkerUpdateUI;
            worker.WorkerSupportsCancellation = true;

            foreach (var btn in editorGrid.Children)
            {
                if (btn is Button)
                {
                    ((Button)btn).IsEnabled = false;
                }
            }

            autoFeed = Settings.Default.APautoFeed;

            mainWindow = Application.Current.Windows[0] as MainWindow;
            ingest = new APingestor();

            RefreshAP(new object(), new RoutedEventArgs());

            frame_Story.Content = new APReaderDocs();

            isLoaded = true;

        }

        private void AssignTopicWorkers()
        {
            bool prevAuto = autoFeed;
            Settings.Default.Reload();
            autoFeed = prevAuto;
            chk_AutoFeed.IsChecked = prevAuto;

            if (Settings.Default.APfollowedTopics == null || Settings.Default.APunfollowedTopics == null)
            {
                Trace.WriteLine("Followed Topics were Null");
                return;
            }
            foreach (APTopic followedTopic in Settings.Default.APfollowedTopics)
            {
                var check = topicWorkers.Find(p => p.assignedTopic.topicID == followedTopic.topicID);
                if (check != null)
                {
                    Trace.WriteLine("Topic Worker Already Assigned - " + check.assignedTopic.topicName);
                }
                else
                {
                    TopicWorker newWorker = new TopicWorker();
                    newWorker.DoWork += TopicWorkerFetch;
                    newWorker.RunWorkerCompleted += TopicWorkerUpdate;
                    newWorker.WorkerSupportsCancellation = true;
                    newWorker.assignedTopic = followedTopic;
                    topicWorkers.Add(newWorker);
                }
            }

            foreach (APTopic unfollowedTopic in Settings.Default.APunfollowedTopics)
            {
                Trace.WriteLine("Checking Unfollowed Topic - " + unfollowedTopic.topicName);
                var uncheck = topicWorkers.Find(pp => pp.assignedTopic.topicID == unfollowedTopic.topicID);
                if (uncheck != null)
                {
                    Trace.WriteLine("Removing TopicWorker - Unfollowed - " + uncheck.assignedTopic.topicName);
                    uncheck.unfollowed = true;
                    topicWorkers.Remove(uncheck);
                    RemoveTopicFromFeed(unfollowedTopic);
                }
                else
                {
                    Trace.WriteLine("No worker assigned to " + unfollowedTopic.topicName);
                }
            }

        }

        private void TopicWorkerFetch(object sender, DoWorkEventArgs e)
        {
            if (worker.CancellationPending)
            {
                e.Cancel = true;
            }

            try
            {
                if (!ingest.isAuthorized)
                {
                    e.Cancel = true;
                    return;
                }
                APObject result = ingest.WorkerGetTopicFeed(e.Argument as APTopic,autoFeed);
                
                if (result != null)
                {
                    e.Result = result;
                }
                else
                {
                    e.Result = null;
                }

            }
            catch
            {
                e.Cancel = true;
            }

        }

        private void TopicWorkerUpdate(object sender, RunWorkerCompletedEventArgs e)
        {
            TopicWorker workerSender = sender as TopicWorker;
            if (e.Cancelled)
            {
                Trace.WriteLine("TopicWorker Stopped Working - " + ((TopicWorker)sender).assignedTopic.topicName);
                return;
            }

            if (e.Result == null)
            {
                Trace.WriteLine("TopicWorker Reported No Results - " + ((TopicWorker)sender).assignedTopic.topicName);
            }
            else if (((APObject)e.Result).isBadRequest)
            {
                TopicWorker worker = sender as TopicWorker;
                worker.unfollowed = true;
                worker.assignedTopic.followed = false;
                worker.Dispose();
                Trace.WriteLine("Bad Request, Disposing TopicWorker - " + ((TopicWorker)sender).assignedTopic.topicName);
            }
            else
            {
                List<APObject> newTopic = new List<APObject>();
                newTopic.Add(e.Result as APObject);
                ProcessFeedItems(newTopic);
            }

            if (((TopicWorker)sender).unfollowed)
            {
                ((TopicWorker)sender).Dispose();
                Trace.WriteLine("Topic Worker Disposing - Topic Unfollowed - " + ((TopicWorker)sender).assignedTopic.topicName);
                return;
            }

            if (autoFeed && workerSender.assignedTopic.followed && !btn_RefreshAP.IsEnabled)
            {
                
                Trace.WriteLine("Restarting TopicWorker " + ((TopicWorker)sender).assignedTopic.topicName);
                ((TopicWorker)sender).RunWorkerAsync(((TopicWorker)sender).assignedTopic);

                if (updateMessageNumber > updateMessages.Length - 1)
                {
                    updateMessageNumber = 0;
                }
                btn_RefreshAP.Content = updateMessages[updateMessageNumber];

                updateMessageNumber++;

            }
            else if (!workerSender.assignedTopic.followed)
            {
                workerSender.unfollowed = true;
            }
        }

        private void WorkerFeedUpdate(object sender, DoWorkEventArgs e)
        {
            if (worker.CancellationPending)
            {
                e.Cancel = true;
            }

            try
            {
                List<APObject> treeItems = new List<APObject>();
                if (autoFeed)
                {
                    ingest.GetFeed(true);
                }
                else
                {

                    ingest.GetFeed();
                }
                
                if (ingest.isAuthorized)
                {
                    foreach (APObject obj in ingest.Items)
                    {
                        treeItems.Add(obj);
                    }
                    e.Result = treeItems;

                }
                else
                {
                    e.Cancel = true;
                }

            }
            catch
            {
                e.Result = null;
            }

        }

        private void WorkerUpdateUI(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                btn_RefreshAP.Content = "API Key Unauthorized";
                btn_RefreshAP.IsEnabled = true;
                progbar.Visibility = Visibility.Collapsed;
                chk_AutoFeed.IsChecked = false;
                autoFeed = false;
                return;
            }

            if (e.Result == null)
            {
                Trace.WriteLine("Main BackgroundWorker Stopped Unexpectedly");
                btn_RefreshAP.Content = " Get Latest Stories";
                btn_RefreshAP.IsEnabled = true;
                progbar.Visibility = Visibility.Collapsed;
                autoFeed = false;
                return;
            }

            ProcessFeedItems((List<APObject>)e.Result);

            if (autoFeed)
            {
                worker.RunWorkerAsync();

                if (topicWorkers.Count == 0)
                {
                    AssignTopicWorkers();
                }

                foreach (TopicWorker topicWorker in topicWorkers)
                {
                    if (!topicWorker.IsBusy && !topicWorker.unfollowed && Settings.Default.APShowTopics)
                    {
                        Trace.WriteLine("Restored Broken TopicWorker - " + topicWorker.assignedTopic.topicName);
                        topicWorker.RunWorkerAsync(topicWorker.assignedTopic);
                    }
                }

                if (updateMessageNumber > updateMessages.Length - 1)
                {
                    updateMessageNumber = 0;
                }
                btn_RefreshAP.Content = updateMessages[updateMessageNumber];

                updateMessageNumber++;

            }
            else
            {
                btn_RefreshAP.Content = " Get Latest Stories";
                btn_RefreshAP.IsEnabled = true;
                progbar.Visibility = Visibility.Collapsed;
            }


        }

        private void StopAllFeeds()
        {
            worker.CancelAsync();
            foreach (TopicWorker work in topicWorkers)
            {
                work.CancelAsync();

                work.Dispose();
            }
            if (worker != null)
            {
                worker.Dispose();
            }
            btn_RefreshAP.Content = " Get Latest Stories";
            btn_RefreshAP.IsEnabled = true;
            progbar.Visibility = Visibility.Collapsed;
        }

        private void RemoveTopicFromFeed(APTopic topic)
        {
            APObject existingTopic = listObjects.FirstOrDefault(op => op.altID == topic.topicID.ToString());
            if (existingTopic != null)
            {
                listObjects.Remove(existingTopic);
            }
        }

        private void ProcessFeedItems(List<APObject> newItems)
        {
            if (newItems.Count == 0)
            {
                Trace.WriteLine("No New Items");
            }

            for (int i = 0; i < newItems.Count; i++)
            {
                APObject newItem = newItems[i];
                APObject existing = listObjects.FirstOrDefault(p => p.altID == newItem.altID);
                if (existing != null)
                {
                    if (newItem.isAssocParent && existing.isAssocParent)
                    {
                        for (int ii = 0; ii < newItem.associations.Count; ii++)
                        {
                            APObject newAssoci = newItem.associations[ii];
                            APObject existAssoci = existing.associations.FirstOrDefault(pp => pp.altID == newAssoci.altID);
                            if (existAssoci != null)
                            {
                                if (newAssoci.version > existAssoci.version)
                                {
                                    Trace.WriteLine("ADD: New Association was newer: " + newAssoci.headline + " Version " + newAssoci.version + " vs Version " + existAssoci.version);
                                    existing.headline = newItem.headline;
                                    existing.associations.Remove(existAssoci);
                                    existing.associations.Insert(0, newAssoci);
                                }
                                else
                                {
                                    Trace.WriteLine("NOTHING: New Association was not newer.");
                                }
                            }
                            else
                            {
                                Trace.WriteLine("ADD: Matching association NOT found for " + newAssoci.headline);
                                existing.associations.Insert(0, newAssoci);
                            }

                            if (newItem.associations.Count == 0)
                            {
                                Trace.WriteLine("NOTHING: All associations used up from new object");
                            }
                        }
                    }
                    else if (newItem.version > existing.version)
                    {
                        Trace.WriteLine("ADD: Found a Match for: " + newItem.headline + " But no associations and it was newer");
                        listObjects.Remove(existing);
                        listObjects.Add(newItem);
                    }
                    else
                    {
                        Trace.WriteLine("NOTHING: Found a Match for: " + newItem.headline + " But it was NOT newer");
                    }
                }
                else if (newItem.isAssocParent)
                {
                    Trace.WriteLine("ADD: New Item Found With Associations: " + newItem.headline);
                    listObjects.Insert(0, newItem);
                }
                else
                {
                    Trace.WriteLine("ADD: New Item Found: " + newItem.headline);
                    listObjects.Add(newItem);
                }
            }

            //listObjects = listObjects.OrderBy(story => story.updateDate)

        }

        private void list_APStories_SelectionChanged()
        {
            if (isLoaded)
            {
                APObject selected = list_APStories.SelectedItem as APObject;
                if (selected != null)
                {
                    if (selected.HasStory)
                    {
                        SetControls(selected.story.isEditMode);
                        if (selected.story.isEditMode)
                        {
                            btn_ToggleMode.Content = "Revert to Original Story";
                        }
                        else
                        {
                            btn_ToggleMode.Content = "Switch to Story Editor Mode";
                        }
                        frame_Story.Content = selected.story;
                        foreach (var btn in editorGrid.Children)
                        {
                            if (btn is Button)
                            {
                                ((Button)btn).IsEnabled = true;
                            }
                        }
                        editorGrid.Visibility = Visibility.Visible;
                        scrl_Story.ScrollToVerticalOffset(0);
                    }
                    else
                    {
                        foreach (var btn in editorGrid.Children)
                        {
                            if (btn is Button)
                            {
                                ((Button)btn).IsEnabled = false;
                            }
                        }
                        frame_Story.Content = new APReaderDocs();
                        //editorGrid.Visibility = Visibility.Collapsed;
                    }
                }
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopAllFeeds();
            worker.CancelAsync();

            if (worker != null)
            {
                worker.Dispose(); 
            }

            if (ingest != null)
            {
                ingest.Dispose();

            }

            mainWindow.apReader = null;
        }

        private void RefreshAP(object sender, RoutedEventArgs e)
        {
            if (freezeFeed)
            {
                if (topicWorkers != null && topicWorkers.Count > 0)
                {
                    foreach (TopicWorker top in topicWorkers)
                    {
                        if (top != null)
                        {
                            top.Dispose();

                        }
                    }
                    topicWorkers.Clear();
                }

                if (frame_Story.Content.GetType() == typeof(APTopicSettings))
                {
                    frame_Story.Content = new APReaderDocs();
                }

                freezeFeed = false;
            }

            AssignTopicWorkers();

            btn_RefreshAP.Content = "Getting Feed...";
            if (chk_AutoFeed.IsChecked == true)
            {
                if (updateMessageNumber > updateMessages.Length - 1)
                {
                    updateMessageNumber = 0;
                }
                btn_RefreshAP.Content = updateMessages[updateMessageNumber];
                autoFeed = true;
            }

            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync();
            }

            foreach (TopicWorker top in topicWorkers)
            {
                if (!top.IsBusy)
                {
                    top.RunWorkerAsync(top.assignedTopic);

                }
            }


            btn_RefreshAP.IsEnabled = false;
            progbar.Visibility = Visibility.Visible;
        }

        private void list_APStories_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.Dispatcher.Invoke(() =>
            {
                list_APStories_SelectionChanged();
            });
        }

        private void btn_ToggleMode_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            APStory story = frame_Story.Content as APStory;
            story.ToggleModes();
            SetControls(story.isEditMode);
            if (story.isEditMode)
            {
                btn.Visibility = Visibility.Visible;
                btn.Content = "Revert to Original Story";
            }
            else
            {
                btn.Visibility = Visibility.Visible;
                btn.Content = "Switch to Story Editor Mode";
            }

        }

        private void SetControls(bool editMode)
        {
            if (editMode)
            {
                Grid.SetRow(scrl_Story, 2);
                Grid.SetRowSpan(scrl_Story, 2);
                Grid.SetRowSpan(editorGrid, 2);

                Grid.SetRow(btn_ToggleMode, 1);
                Grid.SetRowSpan(btn_ToggleMode, 1);
                Grid.SetRowSpan(btn_SendToNewScript, 1);
                Grid.SetRowSpan(btn_SendToScript, 1);

                lbl_EditorDisclaimer.Visibility = Visibility.Visible;
                btn_DeleteChecked.Visibility = Visibility.Visible;
                btn_DeleteUnChecked.Visibility = Visibility.Visible;
            }
            else
            {
                Grid.SetRow(scrl_Story, 1);
                Grid.SetRowSpan(scrl_Story, 3);
                Grid.SetRowSpan(editorGrid, 1);

                Grid.SetRow(btn_ToggleMode, 2);
                Grid.SetRowSpan(btn_ToggleMode, 2);
                Grid.SetRowSpan(btn_SendToNewScript, 2);
                Grid.SetRowSpan(btn_SendToScript, 2);

                lbl_EditorDisclaimer.Visibility = Visibility.Collapsed;
                btn_DeleteChecked.Visibility = Visibility.Collapsed;
                btn_DeleteUnChecked.Visibility = Visibility.Collapsed;
            }
        }

        private void btn_SendToScript_Click(object sender, RoutedEventArgs e)
        {
            APStory story = frame_Story.Content as APStory;
            mainWindow.currentScript.InsertChunksFromIngestor(story.GetChunks());
        }

        private void btn_SendToNewScript_Click(object sender, RoutedEventArgs e)
        {

            APStory story = frame_Story.Content as APStory;
            mainWindow.AddNewTabFromChunks(story.GetChunks());

        }

        private void btn_DeleteChecked_Click(object sender, RoutedEventArgs e)
        {
            APStory story = frame_Story.Content as APStory;
            story.DeleteChecked();
        }

        private void btn_DeleteUnChecked_Click(object sender, RoutedEventArgs e)
        {
            APStory story = frame_Story.Content as APStory;
            story.DeleteUnChecked();
        }

        private void btnTopicsSettings_Click(object sender, RoutedEventArgs e)
        {
            foreach (var btn in editorGrid.Children)
            {
                if (btn is Button)
                {
                    ((Button)btn).IsEnabled = false;
                }
            }

            if (frame_Story.Content.GetType() != typeof(APTopicSettings))
            {
                frame_Story.Content = new APTopicSettings(ingest);
                StopAllFeeds();
                freezeFeed = true;
            }
            
        }

        private void chk_AutoFeed_Checked(object sender, RoutedEventArgs e)
        {
            autoFeed = true;
        }

        private void chk_AutoFeed_Unchecked(object sender, RoutedEventArgs e)
        {
            autoFeed = false;

            StopAllFeeds();
        }
    }
}
