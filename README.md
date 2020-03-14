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
bms2csv.exe -V -P -N[measure] [filepath]	…　通常再生
bms2csv.exe -V -R -N[measure] [filepath]	…　小節単位のリピート再生
bms2csv.exe -V -S				…　何もせずに終了
```
- **measure**
  - 再生を開始する小節
- **filepath**
  - BMSファイルのパス

### Config.ini
1. 「Speed=」の右にハイスピの値を入力
1. 「LoopDisplayNum=」の右に小節リピート時のノーツ表示の繰り返し数を入力する
1. リピート間でポーズする場合は「PauseBeforeLoop=」の右に「1」を入力
1. 「MusicSpeed=」の右に音楽の再生速度の値を入力（0.0を超え、-3.0以下）
1. 音楽の再生速度を変更した際にピッチ補正するなら「CorrectPitch」の右に「1」を入力（音楽の再生速度が0.5以上、-2.0以下の場合に有効）
