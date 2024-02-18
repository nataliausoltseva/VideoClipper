# Video Clipper

![Light theme screenshot](./screenshots/app-view.png)

## Instructions:
1. Select a video you would like to clip.
2. Specify the timespans for start and end.
3. Press the clip button. It might take a while depending on the length/size of the video since it is re-encoding the whole video. Once it is re-encoded, you will be able to preview the clipped video in the app. The app also indicates the location of the file and its filename.

## Future improvements:
1. Validtion for timespans.
1.1. If no start is provided, it should default to the start of the video.
1.2. If no end is provided, it should default to the end of the video.
1.3. If incorrect format is provided, it should be indicated to a user.
2. Options for encoding
2.1. VideoCodec
2.2. AudioCodec
2.3. ConstantRateFactor
2.4. VariableBitrate
3. Options for copying video and audio - if a user decideds to copy a video it is going to speed up the clipping process.
4. Options for clipping a video based on the length from the starting timespan. The end timespan is not required in this case.