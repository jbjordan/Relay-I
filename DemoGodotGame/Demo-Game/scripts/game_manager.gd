extends Node

var score = 0

@onready var score_label = $ScoreLabel

func add_point():
	score += 1
	score_label.text = "You collected " + str(score) + " coins."
	var relay_log_dict = { "Event" : "CoinCount", "TotalCoins" : str(score), "LevelId" : "1"  }
	GlobalLogger.log_game_event(relay_log_dict)
