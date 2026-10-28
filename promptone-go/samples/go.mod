module github.com/prompttechies-del/promptone/promptone-go/samples

go 1.22

require (
	github.com/prompttechies-del/promptone/promptone-go v0.0.0-00010101000000-000000000000
	github.com/prompttechies-del/promptone/promptone-go/onnx v0.0.0-00010101000000-000000000000
)

require github.com/yalue/onnxruntime_go v1.36.0 // indirect

replace (
	github.com/prompttechies-del/promptone/promptone-go => ../
	github.com/prompttechies-del/promptone/promptone-go/onnx => ../onnx
)
