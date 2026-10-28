# Fine-tuning

`promptone.train` is imported on its own (`from promptone.train import finetune, TrainConfig`); it is not
re-exported from the top-level package. `promptone-train` is its command line. The
[fine-tuning guide](../finetune.md#fine-tune-with-promptone-train) shows both in use.

## Running a fine-tune

::: promptone.train.TrainConfig

::: promptone.train.finetune

::: promptone.train.dry_run

## The two halves of `finetune`

::: promptone.train.train_model

::: promptone.train.calibration_records

::: promptone.train.calibration_report

## Reading data

::: promptone.train.read_data

::: promptone.train.rows_from_csv

## Building training items

::: promptone.train.items_from_rows

::: promptone.train.target_from_gold

::: promptone.train.target_from_expected

::: promptone.train.to_internal

::: promptone.train.make_item

::: promptone.train.encode_item

::: promptone.train.draw_option_order

::: promptone.train.split_calibration

## Losses

::: promptone.train.soft_ce_loss

::: promptone.train.rlcd_loss

## Checkpoints

::: promptone.train.resolve_checkpoint_dir

::: promptone.train.load_checkpoint

::: promptone.train.save_checkpoint
