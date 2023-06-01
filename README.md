# Reddit ScreenshotGifBot

This is a Reddit bot I made that will convert a .gif into its individual image frames and upload the images as .jpg files to Imgur and Google Drive. The bot is activated by directly mentioning its username “u/ScreenshotGifBot” via a comment on Reddit (*NOTE: it appears the bot got banned by Reddit after finishing this project, so it no longer works via this user*). Replies to a comment made by the bot will not activate it. The bot accepts URL links as input and will download the .gif from the linked URL.

The bot will first search the mentioning comment for a valid URL. If the mentioning comment does not contain a URL, it will search the parent object (either the Reddit post or the comment that the mentioning comment was responding to) for a URL. If the comment contains more than one URL, only the first URL is used. This application runs in a continuous loop with a minimum 30 second delay between each check for new username mentions on Reddit. 

This bot can only work with .gif and .gifv files. It cannot work with .mp4 files. In general, if the file you are linking to has audio or you can see a video player interface when you mouse over it, this bot will not work. 

Reddit spam filters and the currently low karma rating for the bot account can prevent it from being used on certain subreddits. All testing has been completed in the subreddit “/r/ScreenshotGifBot/” and the bot should work without issue if you wish to use it there.

The bot will reply with links to an Imgur album and a Google Drive folder containing the .jpg images. Imgur has an API limit of 50 uploads/hour, so to minimize the likelihood of exceeding this limit, only the first five extracted images are uploaded to Imgur. The Google Drive folder contains all images. 

A 20 MB file size limit exists for the .gif file, if it exceeds this limit it will not be processed. Any extracted .jpg images larger than 5MB are skipped and not uploaded. 

The program can be sped up by changing the GoogleDriveUtility.UploadImagesAsync method to operate asynchronously. It has been set to operate synchronously as during testing I found that images would randomly not get uploaded to Google Drive when using the async methods. To change it to async mode, uncomment all of the lines in the method and comment out the Upload method call.
