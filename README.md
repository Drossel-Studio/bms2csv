# bms2csv
BMSをCSVに変換するすごいやつ 

## 使い方
```
bms2csv.exe [path] [outputpath]
```
- **path**
  - BMSファイルのあるパス
- **outputpath**
  - CSVとmetaファイルを出力するパス

## ビューアモード
```
bms2csv.exe -V -P -N[measure] [filepath] [exename]	…　通常再生
bms2csv.exe -V -R -N[measure] [filepath]  [exename]	…　小節単位のリピート再生
bms2csv.exe -V -S					…　何もせずに終了
```
- **measure**
  - 再生を開始する小節
- **filepath**
  - BMSファイルのパス
- **exename**
  - 起動するEXEファイル名（bms2csv.exeからの相対パス）

### Config.ini
1. 「Speed=」の右にハイスピの値を入力
1. 「LoopDisplayNum=」の右に小節リピート時のノーツ表示の繰り返し数を入力する
1. リピート間でポーズする場合は「PauseBeforeLoop=」の右に「1」を入力
1. 「MusicSpeed=」の右に音楽の再生速度の値を入力（0.0を超え、-3.0以下）
1. 音楽の再生速度を変更した際にピッチ補正するなら「CorrectPitch」の右に「1」を入力（音楽の再生速度が0.5以上、-2.0以下の場合に有効）
1. 「BGMVolume」の右にBGMの音量、「SEVolume」の右にSEの音量を入力（0から100まで整数値）
