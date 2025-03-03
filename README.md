# Community.PowerToys.Run.Plugin.Dictionary

**A PowerToys Run plugin for quick and efficient dictionary searches.**

![Dictionary example Search](https://github.com/user-attachments/assets/4c6cba3d-5872-45c9-8cf5-9e5a3fe65418)

## Table of Contents

- [Installation](#installation)
- [Compiling the Plugin](#compiling-the-plugin)
- [Usage](#usage)
- [Acknowledgments](#Acknowledgments)
- [Contributing](#contributing)
- [License](#license)

## Installation

To install the plugin quickly, download the precompiled binaries from the [Release section](https://github.com/Divyanshu-singh15/DictionaryPowerToys/releases).

### Steps:
1. Navigate to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins`.
2. Unpack the downloaded binaries into this directory.
3. **Note:** If you are on a Windows machine, make sure to download the x64 release.

## Compiling the Plugin

If you prefer to compile the plugin manually, follow these steps:

1. Clone the repository to your local machine:
   ```bash
   git clone https://github.com/Divyanshu-singh15/DictionaryPowerToys.git
   ```
2. Open the project in Visual Studio.
3. Ensure that the configuration is set to **x64** or **ARM64** based on your device.
4. **Important:** The compilation requires `Dictionary.db`, which is available in the release binaries. Place this file in the correct directory before running the plugin.

## Usage

Once installed, use PowerToys Run (Alt + Space) and type your query prefixed with the trigger keyword. You can set up your own keyword in the PowerToys Run settings; by default, this plugin uses the backtick (`) as the trigger.

## Acknowledgments

A big thank you to the creators and contributors of the [Wordset Dictionary](https://github.com/wordset/wordset-dictionary) for providing the dictionary data used in this plugin. Your work is greatly appreciated!
Additionally, a special thanks to [Dictionary.com](https://www.dictionary.com/) for serving as the online search provider, helping users find definitions when offline results aren't enough.

## Contributing

Contributions are welcome! Feel free to fork the repository, create a branch, and submit a pull request.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.

