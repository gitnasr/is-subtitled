import os
import fnmatch
import json
from collections import defaultdict
from gooey import Gooey, GooeyParser
CONFIG_FILE = "config.json"

class SubtitleChecker:
    def __init__(self, path: str, excluded_dirs=None, video_extensions=None) -> None:
        self.path = path
        self.videos = []
        self.subtitles = []
        self.video_extensions = video_extensions or ['*.mp4', '*.mkv', '*.flv', '*.avi', '*.mov', '*.wmv', '*.ts']
        self.excluded_dirs = excluded_dirs or ["COMP"]

    def get_files(self):
        for dirpath, dirnames, filenames in os.walk(self.path):
            dirnames[:] = [d for d in dirnames if d not in self.excluded_dirs]
            for filename in filenames:
                if any(fnmatch.fnmatch(filename, ext) for ext in self.video_extensions):
                    self.videos.append(os.path.join(dirpath, filename))
                elif fnmatch.fnmatch(filename, "*.srt"):
                    self.subtitles.append(os.path.join(dirpath, filename))

    def difference(self):
        subtitle_bases = {os.path.splitext(os.path.basename(subtitle))[0] for subtitle in self.subtitles}
        videos_without_subtitles = [video for video in self.videos if os.path.splitext(os.path.basename(video))[0] not in subtitle_bases]
        return videos_without_subtitles

def save_to_file(file_path, videos_without_subtitles):
    grouped_videos = defaultdict(list)
    # sort videos by directory and by video name
    videos_without_subtitles.sort(key=lambda x: (os.path.dirname(x), os.path.basename(x)))
    for video in videos_without_subtitles:
        dir_name = os.path.dirname(video)
        grouped_videos[dir_name].append(video)
    
    with open(file_path, 'w', encoding="utf-8") as f:
        for dir_name, videos in grouped_videos.items():
            f.write(f"----------------- {dir_name} ------------------\n")
            for video in videos:
                f.write(video + '\n')
            f.write('\n')
        f.write(f"Total: {len(videos_without_subtitles)}\n")
        
def load_last_session():
    if os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
            return json.load(f)
    return {}

def save_last_session(config):
    with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
        json.dump(config, f)
@Gooey(program_name="Subtitle Checker", clear_before_run=True)
def main():
    last_session = load_last_session()
    
    parser = GooeyParser(description="Check videos for missing subtitles")
    parser.add_argument('path', widget='DirChooser', help="Select the main directory path", default=last_session.get('path', ''))
    parser.add_argument('excluded_dirs', widget='TextField', help="Select directories to exclude (separated by semicolons)", default="".join(last_session.get('excluded_dirs', [])))
    parser.add_argument('output_file', widget='FileSaver', help="Select the output file path", default="NoSub.txt")
    args = parser.parse_args()

    # Save current session settings
    config = {
        'path': args.path,
        'excluded_dirs': args.excluded_dirs
    }
    save_last_session(config)
    
    app = SubtitleChecker(args.path, excluded_dirs=args.excluded_dirs)
    app.get_files()
    videos_without_subtitles = app.difference()
    save_to_file(args.output_file, videos_without_subtitles)

if __name__ == "__main__":
    main()
