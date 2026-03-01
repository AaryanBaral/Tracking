(function () {
  let pipElement = null;
  let playing = false;
  let observerDebounce = null;

  function domainFromUrl(url) {
    try {
      return new URL(url).hostname;
    } catch {
      return null;
    }
  }

  function postState(reason) {
    const video = pipElement || document.pictureInPictureElement || document.querySelector("video");
    const videoUrl = video?.currentSrc || video?.src || window.location.href;
    const payload = {
      type: "pip_state",
      reason,
      pipActive: Boolean(document.pictureInPictureElement),
      isVideoPlaying: playing,
      videoUrl: videoUrl || null,
      videoDomain: domainFromUrl(videoUrl || window.location.href),
      title: document.title,
      timestamp: new Date().toISOString()
    };

    chrome.runtime.sendMessage(payload, () => {
      void chrome.runtime.lastError;
    });
  }

  function attachVideoListeners(video) {
    if (!video || video.dataset.employeeTrackerBound === "1") {
      return;
    }

    video.dataset.employeeTrackerBound = "1";
    video.addEventListener("play", () => {
      playing = true;
      postState("play");
    });

    video.addEventListener("pause", () => {
      playing = false;
      postState("pause");
    });

    video.addEventListener("enterpictureinpicture", (event) => {
      pipElement = event.target;
      playing = !event.target.paused;
      postState("enter_pip");
    });

    video.addEventListener("leavepictureinpicture", () => {
      pipElement = null;
      postState("leave_pip");
    });
  }

  function bindAllVideos() {
    const videos = document.querySelectorAll("video");
    videos.forEach((video) => attachVideoListeners(video));
  }

  const observer = new MutationObserver(() => {
    if (observerDebounce) {
      clearTimeout(observerDebounce);
    }

    observerDebounce = setTimeout(() => {
      bindAllVideos();
      observerDebounce = null;
    }, 1000);
  });

  observer.observe(document.documentElement, { childList: true, subtree: true });

  bindAllVideos();
  setInterval(() => postState("heartbeat"), 15000);
})();
