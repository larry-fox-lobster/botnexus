// BotNexus Blazor Client — Audio recording via MediaRecorder
window.BotNexusAudio = (function () {
    var mediaRecorder = null;
    var audioChunks = [];
    var stream = null;

    return {
        /**
         * Starts recording audio from the default microphone.
         * Returns true if recording started successfully, false otherwise.
         */
        startRecording: async function () {
            try {
                stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                audioChunks = [];
                mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });
                mediaRecorder.ondataavailable = function (e) {
                    if (e.data.size > 0) {
                        audioChunks.push(e.data);
                    }
                };
                mediaRecorder.start();
                return true;
            } catch (err) {
                console.error('BotNexusAudio: Failed to start recording', err);
                return false;
            }
        },

        /**
         * Stops recording and returns the audio as a base64-encoded string.
         * Returns null if no recording was in progress.
         */
        stopRecording: function () {
            return new Promise(function (resolve) {
                if (!mediaRecorder || mediaRecorder.state === 'inactive') {
                    resolve(null);
                    return;
                }
                mediaRecorder.onstop = function () {
                    var blob = new Blob(audioChunks, { type: 'audio/webm' });
                    var reader = new FileReader();
                    reader.onloadend = function () {
                        // Strip the data URL prefix to get pure base64
                        var base64 = reader.result.split(',')[1];
                        resolve(base64);
                    };
                    reader.readAsDataURL(blob);
                    // Stop all tracks to release the microphone
                    if (stream) {
                        stream.getTracks().forEach(function (t) { t.stop(); });
                        stream = null;
                    }
                    mediaRecorder = null;
                    audioChunks = [];
                };
                mediaRecorder.stop();
            });
        },

        /**
         * Cancels the current recording without returning data.
         */
        cancelRecording: function () {
            if (mediaRecorder && mediaRecorder.state !== 'inactive') {
                mediaRecorder.stop();
            }
            if (stream) {
                stream.getTracks().forEach(function (t) { t.stop(); });
                stream = null;
            }
            mediaRecorder = null;
            audioChunks = [];
        },

        /**
         * Returns whether a recording is currently in progress.
         */
        isRecording: function () {
            return mediaRecorder !== null && mediaRecorder.state === 'recording';
        }
    };
})();
