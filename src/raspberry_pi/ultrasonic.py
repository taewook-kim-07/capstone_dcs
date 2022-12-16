# -*- coding: utf8 -*-
import RPi.GPIO as GPIO
import time
from multiprocessing import Process, Value

#### 초음파 센서
#거리 타임 아웃 용
MAX_DISTANCE_CM = 300
MAX_DURATION_TIMEOUT = (MAX_DISTANCE_CM * 2 * 29.1) #17460 # 17460us = 300cm

class Ultrasonic:
    def __init__(self, TrigerPin, EchoPin):
        self.distance = Value('d', 0)
        self.TrigerPin = TrigerPin
        self.EchoPin = EchoPin
        
        GPIO.setup(self.EchoPin, GPIO.IN)
        GPIO.setup(self.TrigerPin, GPIO.OUT, initial=GPIO.LOW)

        p1 = Process(target=self.MeasureDistance, args=(self.distance, ))
        p1.daemon = True
        p1.start()

    def MeasureDistance(self, distance):
        while True:            
            self.fail = False
            time.sleep(0.1)
            
            # 트리거를 10us 동안 High 했다가 Low로 함.
            GPIO.output(self.TrigerPin, GPIO.HIGH)
            time.sleep(0.00001)
            GPIO.output(self.TrigerPin, GPIO.LOW)

            # ECHO로 신호가 들어 올때까지 대기
            self.timeout = time.time()
            while GPIO.input(self.EchoPin) == GPIO.LOW:
                #들어왔으면 시작 시간을 변수에 저장
                self.pulse_start = time.time()
                if ((self.pulse_start - self.timeout)*1000000) >= MAX_DURATION_TIMEOUT:
                    #171206 중간에 통신 안되는 문제 개선용        
                    self.fail = True
                    break

            if self.fail:
                continue
                
            #ECHO로 인식 종료 시점까지 대기
            while GPIO.input(self.EchoPin) == GPIO.HIGH:
                #종료 시간 변수에 저장
                self.pulse_end = time.time()
                if ((self.pulse_end - self.pulse_start)*1000000) >= MAX_DURATION_TIMEOUT:
                    #171206 중간에 통신 안되는 문제 개선용               
                    self.fail = True
                    break
                    
            if self.fail:
                continue
                
            #인식 시작부터 종료까지의 차가 바로 거리 인식 시간
            self.pulse_duration = (self.pulse_end - self.pulse_start) * 1000000

            # 시간을 cm로 환산
            self.distance = (self.pulse_duration/2)/29.1
            distance.value = int(self.distance)

    def getDistance(self):
        return self.distance.value