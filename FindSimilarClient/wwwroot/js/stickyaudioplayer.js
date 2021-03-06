/*
 * Sticky Audio Player - jQuery Plugin
 * Responsive Sticky Audio Player
 *
 * Examples and documentation at: http://tiendasdigitales.net
 *
 * Copyright (c) 2017 Lucas Gabriel Martinez
 *
 * Version: 1.0.0 - 2017/07/26
 * Requires: jQuery v1.3+
 *
 * Dual licensed under the MIT and GPL licenses:
 *   http://www.opensource.org/licenses/mit-license.php
 *   http://www.gnu.org/licenses/gpl.html
 */
$(document).ready(function (e) {

	$.fn.stickyAudioPlayer = function (options) {
		var $this = this;
		var player = this; // used as a dispatcher for the audio player eventlisteners

		var params = $.extend({
			url: options.url,
			position: options.position ? options.position : 'bottom',
			text: options.text ? options.text : null,
			image: options.image ? options.image : false,
			maxWidth: options.maxWidth ? options.maxWidth : 1200,
			repeat: options.repeat ? options.repeat : false,
			volume: options.volume ? options.volume : 100,
			download: options.download ? options.download : false,
			theme: options.theme ? options.theme : 'default'
		}, options);

		var randomId = 'player' + new Date().getTime();
		var container = $(this);
		var fileLength = 0;
		var timeProgress = 0;
		var volume = params.volume;
		var animationSpeed = 600;
		var boxHeightRemember = 0;
		var isRunning = false;
		var box, boxContainer, audioElement, btnPause, btnPlay, btnStop, textTile, barMain, barProgress, boxCover, textSong, boxData, proceedTo, btnVolume, btnMute, barVolume, barVolumeProgress, maxWidthBarVolume, boxDownload, boxDownloadLink, btnFloat;

		var setVars = function () {
			box = $('#' + randomId);
			boxContainer = $('#' + randomId).find('.stickyAudioPlayerBoxContainer');
			audioElement = document.createElement('audio');
			btnPause = $('#' + randomId).find('.player-play .input-pause');
			btnPlay = $('#' + randomId).find('.player-play .input-play');
			btnStop = $('#' + randomId).find('.player-play .input-stop');
			textTile = $('#' + randomId).find('.bar-container .bar-tile');
			barMain = $('#' + randomId).find('.player-data .bar-container .bar-main');
			barProgress = $('#' + randomId).find('.player-data .bar-container .bar-main .bar-progress');
			boxCover = $('#' + randomId).find('figure img');
			textSong = $('#' + randomId).find('.player-data p');
			boxData = $('#' + randomId).find('.player-data');
			btnVolume = $('#' + randomId).find('.player-volume .input-volume');
			btnMute = $('#' + randomId).find('.player-volume .input-mute');
			barVolume = $('#' + randomId).find('.player-volume .bar-container .bar-main');
			barVolumeProgress = $('#' + randomId).find('.player-volume .bar-container .bar-main .bar-progress')
			proceedTo = 0;
			boxDownload = $('#' + randomId).find('.player-download');
			boxDownloadLink = $('#' + randomId).find('.player-download a');

			barMain.css({ position: 'relative' }); // fix
			boxHeightRemember = box.innerHeight();
			if (params.position == 'bottom' || params.position == 'top') {
				box.css({ height: 0 });
				btnFloat = $('.stickyAudioPlayerBoxFloatingButton');
			} else {
				box.css({ height: 'auto' });
			}
		}

		var createAudio = function (file) {

			if (file.length > 0) {
				audioElement.setAttribute('src', file);

				audioElement.addEventListener('ended', function () {
					if (params.repeat) {
						play();
					} else {
						stop();
					}
					player.trigger("ended");
				}, false);

				audioElement.addEventListener("canplay", function () {
					fileLength = audioElement.duration; //	Seconds
					console.log("Source:" + audioElement.src);
					console.log("Status: Ready to play");
					if (!isRunning) {
						open();
						isRunning = true;
					}
					player.trigger("canplay");
				});

				audioElement.addEventListener("timeupdate", function () {
					timeProgress = audioElement.currentTime;
					barProgressSize();
					var currentMin = Math.floor(audioElement.currentTime / 60);
					var currentSeconds = Math.round(Math.abs((currentMin * 60) - audioElement.currentTime));
					var timelapse = (currentMin < 10 ? '0' + currentMin : currentMin) + ':' + (currentSeconds < 10 ? '0' + currentSeconds : currentSeconds);
					$('#' + randomId).find(".player-data .bar-container small").html(timelapse);
					player.trigger("timeupdate");
				});
			}
		}

		var init = function () {

			createHtml();
			setVars();
			setBoxPosition();

			// Download url
			if (!params.download) {
				boxDownload.css({ display: 'none' });
			} else {
				boxDownloadLink.attr('href', params.download);
			}

			createAudio(params.url);

			btnPlay.click(function () {
				play();
			});

			btnPause.click(function () {
				pause();
			});

			btnStop.click(function () {
				stop();
			});

			barMain.mousemove(function (event) {
				tileProgress(event);
			});

			barMain.click(function () {
				// Proceed & Play
				pause();
				audioElement.currentTime = proceedTo;
				play();
			});

			btnVolume.click(function () {
				soundOn();
			});

			btnMute.click(function () {
				mute();
			});

			if (params.position == 'bottom' || params.position == 'top') {
				btnFloat.click(function () {
					floatClickHide();
				});
			}
			
			setCover();
			setSongName();
			setMaxWidth();
			setVolume(volume);
			open();

			barVolume.mousemove(function (event) {
				volumeProgress(event);
			});
		}

		var floatClickHide = function () {
			hide();
			btnFloat.unbind("click");
			btnFloat.find('.input-go-down').toggleClass('input-go-down input-go-up');
			btnFloat.click(function () {
				floatClickShow();
			});
		}

		var floatClickShow = function () {
			show();
			btnFloat.unbind("click");
			btnFloat.find('.input-go-up').toggleClass('input-go-up input-go-down');
			btnFloat.click(function () {
				floatClickHide();
			});
		}

		var setMaxWidth = function () {
			boxContainer.css({ maxWidth: params.maxWidth, float: 'none' });
		}

		var setSongName = function () {
			if (params.text == null) {
				textSong.html('<span>' + getFileName(params.url) + '</span>');
			} else {
				textSong.html('<span>' + params.text + '</span>');
			}
		}

		var getFileName = function (path) {
			path = path.substring(path.lastIndexOf("/") + 1);
			return (path.match(/[^.]+(\.[^?#]+)?/) || [])[0];
		}

		var setBoxPosition = function () {
			if (params.position == 'bottom' || params.position == 'top') {
				box.css({ position: 'fixed', bottom: 0, left: 0, top: $(window).height() });
				btnFloat.css({ opacity: 0, position: 'fixed', 
					left: $(window).width() - btnFloat.width() - 30, 
					bottom: 0, 
					top: $(window).height() - boxHeightRemember - btnFloat.height() });
				btnFloat.animate({ opacity: 1 }, animationSpeed);
			}
			maxWidthBarVolume = $('#' + randomId).find('.player-volume .bar-container .bar-main').innerWidth();
		}

		var setCover = function () {
			if (!params.image && (params.position == 'bottom' || params.position == 'top')) {
				boxCover.css({ display: 'none' });
			} else {
				boxCover.css({ display: 'block' });
				boxCover.attr('src', !params.image ? '' : params.image);
			}
		}

		var barProgressSize = function () {
			var width = barMain.width();
			var xPercent = (timeProgress * 100) / fileLength;
			var nWidth = (xPercent * width) / 100;
			barProgress.css({ width: nWidth });
		}

		var tileProgress = function (event) {
			var width = barMain.width();
			var mousePosition = event.pageX - barProgress.offset().left;
			var xPercent = (mousePosition * 100) / width;
			var selectedTime = (xPercent * fileLength) / 100;
			var promiseMin = Math.floor(selectedTime / 60);
			var promiseSeconds = Math.round(Math.abs((promiseMin * 60) - selectedTime));
			textTile.html((promiseMin < 10 ? '0' + promiseMin : promiseMin) + ':' + (promiseSeconds < 10 ? '0' + promiseSeconds : promiseSeconds));
			textTile.css({ position: 'absolute', left: mousePosition - 15, top: '-25px' });
			proceedTo = selectedTime;
		}

		var volumeMouseOver = 100;
		var volumeProgress = function (event) {
			var width = barVolume.innerWidth();
			/* Fix width, overflow limit */
			var mousePosition = event.pageX - barVolumeProgress.offset().left;
			var xPercent = (mousePosition * 100) / width;
			volumeMouseOver = (xPercent * 100) / 100;

			barVolume.click(function () {
				setVolume(volumeMouseOver);
			});
		}
		
		var stop = function () {
			audioElement.currentTime = 0;
			audioElement.pause();
			btnPlay.css({ display: 'inline-block' });
			btnPause.css({ display: 'none' });
			btnStop.css({ display: 'none' });
		}

		var play = function () {
			if (audioElement.src.length > 0) { 
				audioElement.play();
				btnPlay.css({ display: 'none' });
				btnPause.css({ display: 'inline-block' });
				btnStop.css({ display: 'none' }); // optional
			}
		}

		var pause = function () {
			audioElement.pause();
			btnPlay.css({ display: 'inline-block' });
			btnPause.css({ display: 'none' });
			btnStop.css({ display: 'none' }); // optional
		}

		var mute = function () {
			btnVolume.css({ display: 'block' });
			btnMute.css({ display: 'none' });
			setVolume(volume);
		}

		var soundOn = function () {
			btnVolume.css({ display: 'none' });
			btnMute.css({ display: 'block' });
			audioElement.volume = 0;
		}

		var setVolume = function (val) {
			var width = barVolume.innerWidth();
			var nWidth = (val * width) / 100;
			if (nWidth > maxWidthBarVolume) {
				nWidth = maxWidthBarVolume;
			}
			barVolumeProgress.css({ width: nWidth });
			audioElement.volume = val == 0 ? 0 : (val / 100);
			volume = val;
		}

		var remove = function () {
			if (params.position == 'bottom' || params.position == 'top') {
				var goTo = boxHeightRemember + box.position().top;
				box.animate({ height: 0, top: goTo }, animationSpeed, '', function () { createAudio(''); stop(); box.remove(); });
			} else {
				box.animate({ opacity: 0 }, animationSpeed, '', function () { createAudio(''); stop(); box.remove(); });
			}
		}

		var hide = function () {
			if (params.position == 'bottom' || params.position == 'top') {
				var goTo = $(window).height();
				btnFloat.animate({ top: goTo - btnFloat.height() }, animationSpeed);
				box.animate({ height: 0, top: goTo }, animationSpeed, '', function () { box.css({ display: 'none' }); });
			} else {
				box.animate({ opacity: 0 }, animationSpeed, '', function () { box.css({ display: 'none' }); });
			}
		}

		var show = function () {
			if (params.position == 'bottom' || params.position == 'top') {
				box.css({ display: 'block' });
				var goTo = $(window).height() - boxHeightRemember;
				btnFloat.animate({ top: goTo - btnFloat.height() }, animationSpeed);
				box.animate({ height: boxHeightRemember + (box.innerHeight() - box.height()) + (box.outerHeight() - box.innerHeight()), top: goTo }, animationSpeed, '', function () { });

			} else {
				box.css({ display: 'block' });
				box.animate({ opacity: 1 }, animationSpeed, '', function () { });
			}
		}

		var open = function () {
			show();
		}

		var createHtml = function () {
			var html = '<section class="' + (params.theme == 'default' ? 'stickyAudioPlayerBox' : 'stickyAudioPlayerBox' + params.theme) + '" id="' + randomId + '">\
				<div class="stickyAudioPlayerBoxContainer">\
					<figure>\
						<img src="/images/cover.png" alt="" />\
					</figure>\
					<section class="player-play">\
						<div class="input-pause"></div>\
						<div class="input-stop"></div>\
						<div class="input-play"></div>\
					</section>\
				  <section class="player-data">\
							<p>Loading...</p>\
						<div class="bar-container">\
							<div class="bar-main">\
								<div class="bar-tile">\
									00:00\
								</div>\
								<div class="bar-progress"></div>\
							</div>\
							<small>00:00</small>\
						</div>\
					</section>\
					<section class="player-volume">\
						<div class="input-volume"></div>\
						<div class="input-mute"></div>\
						<div class="bar-container">\
							<div class="bar-main">\
								<div class="bar-progress"></div>\
							</div>\
						</div>\
					</section>\
					<section class="player-download">\
						<a href="" target="_blank">\
							<div class="input-download"></div>\
						</a>\
					</section>\
				</div>\
			</section>'+ (params.position == 'bottom' || params.position == 'top' ?
					'<section class="stickyAudioPlayerBoxFloatingButton">\
				<div class="input-go-down"></div>\
			</section>': '');
			
			if (params.position == 'bottom' || params.position == 'top') {
				container.append(html);
			} else {
				container.html(html);
			}
		}

		$(window).on('resize', function () {
			if (params.position == 'bottom' || params.position == 'top') {
				box.css({ top: $(window).height() - box.height(), position: 'fixed' });
				setMaxWidth();
				btnFloat.css({ opacity: 1, left: $(window).width() - btnFloat.width() - 30, top: $(window).height() - box.height() - btnFloat.height() });
				barProgressSize();
			}
		});

		$(window).on('scroll', function () {

		});

		init();

		return {
			changeAudio: function (url, text, image) {
				stop();
				createAudio(url);
				textSong.html('<span>' + text + '</span>');
				boxCover.attr('src', image);
				play();
				mute();
			},
			play: function () {
				play();
			},
			pause: function () {
				pause();
			},
			stop: function () {
				stop();
			},
			setVolume: function (int) {
				setVolume(int);
			},
			remove: function () {
				remove();
			},
			hide: function () {
				hide();
			},
			show: function () {
				show();
			},
			mute: function () {
				soundOn();
			},
			unmute: function () {
				mute();
			},
			open: function () {
				open();
			},
			player			
		}
	}
});
