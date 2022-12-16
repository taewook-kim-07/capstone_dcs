#!/usr/bin/env python
# -*- coding: utf8 -*-
import RPi.GPIO as GPIO
import time
import threading
import os
import sys, signal

from pyserver.network import *  # https://github.com/juhgiyo/pyserver
import ultrasonic, dcMotor, servoMotor, dht11

#################### GLOBAL VARIABLE #########################      
"""
    DC모터, 서보모터, 초음파센서(공통 Trigger, 개별 Echo), 온습도센서의 Pin 번호를 구성하는 변수들
"""
#### PIN 설정
# DC모터

MotorPinA_EN = 26
MotorPinA_IN1 = 19
MotorPinA_IN2 = 13

MotorPinB_EN  = 0
MotorPinB_IN1 = 6
MotorPinB_IN2 = 5

# 서보모터
VServoMotorPin = 25 # 180도
HServoMotorPin = 8  # 300도

# 초음파센서 Echo가 추가되면 us_prevDis 수정
TrigerPin1 = 20
EchoPin1 = 23
TrigerPin2 = 21
EchoPin2 = 24

# 온습도 센서
TempPin = 15


"""
    UDP_LastRecvTime: UDP 수신에서 모터 제어가 이뤄진 마지막 시간
    위 변수로 주기적으로 받아지는 UDP 제어 신호가 어던 문제가 발생하여 수신이 이뤄지지 않는 지 판단 가능
    
    ReturningMode: 정해진 시간동안 UDP 수신이 정상적으로 이뤄지지 않으면 자동 복귀 모드로 전환되는 변수들
    Waypoints: 모터의 "좌, 우, 앞, 뒤"를 몇 초동안 움직였는 지 기록하는 변수 (주기적으로 저장되는 것이 아닌 이동 방향이 변동될 때 마다 저장됨)
"""
#### 기타
UDP_LastRecvTime = 0.0  # UDP 마지막으로 받은 시간
ReturningMode = False   # 드론 복귀 모드 
Waypoints = []          # 복귀를 위한 Waypoint 변수 선언 [ [이동방향, 시간], ... ]

################## GPIO initailization ###########################
"""
    DCLeft, DCRight에 dcmotor.py에 있는 DCMotor Class를 생성해서 DC모터를 구동하기 위한 기본적인 초기화를 함
    ultrasonic_trigger은 ultrasonic.py에 있는 Class를 사용하며 공통으로 묶어진 Trigger로 사용해 동시에 Ping을 보냄
    ultrasonic은 ultrasonic.py의 Ultrasonic class 생성을 한다. 또, Echo Pin을 사용하여 ISR 방식으로 거리 측정을 함
    servoMotor은 servoMotor.py의 서보모터 제어를 위한 Calss 생성 및 초기화
    temperature dht11.py의 DHT11 제어를 위한 Class 생성 및 초기화
"""
##### 초기 설정
print('GPIO Setup')
GPIO.setmode(GPIO.BCM)
GPIO.setwarnings(False)

# DC모터
DCLeft  = dcMotor.DCMotor(MotorPinA_EN, MotorPinA_IN1, MotorPinA_IN2)
DCRight = dcMotor.DCMotor(MotorPinB_EN, MotorPinB_IN1, MotorPinB_IN2)

# 초음파
ultrasonic1 = ultrasonic.Ultrasonic(TrigerPin1, EchoPin1)
ultrasonic2 = ultrasonic.Ultrasonic(TrigerPin2, EchoPin2)

# 서보모터
servoMotor1 = servoMotor.ServoMotor(VServoMotorPin, 0) # 180도
servoMotor2 = servoMotor.ServoMotor(HServoMotorPin, 1) # 300도

# DHT11 온습도계
temperature = dht11.DHT11(TempPin)

##### DCMotor 제어
"""
    DCMotor_Set은 UDP 신호가 들어올때 호출되거나 복귀 모드를 사용할 때 호출하기 위해 별도로 함수를 선언함
    DCMotor_Set('F') 를 사용하면 앞으로 전진함
"""
def DCMotor_Set(data):
    if data == 'S':
        DCLeft.setMotor(100, dcMotor.STOP)
        DCRight.setMotor(100, dcMotor.STOP)
    elif data == 'F':
        DCLeft.setMotor(100, dcMotor.FORWARD)
        DCRight.setMotor(100, dcMotor.FORWARD)
    elif data == 'B':
        DCLeft.setMotor(100, dcMotor.BACKWARD)
        DCRight.setMotor(100, dcMotor.BACKWARD)
    elif data == 'L':
        DCLeft.setMotor(100, dcMotor.FORWARD)
        DCRight.setMotor(100, dcMotor.BACKWARD)
    elif data == 'R':
        DCLeft.setMotor(100, dcMotor.BACKWARD)
        DCRight.setMotor(100, dcMotor.FORWARD)
    elif data == 'FR':
        DCLeft.setMotor(20, dcMotor.FORWARD)
        DCRight.setMotor(100, dcMotor.FORWARD)
    elif data == 'FL':
        DCLeft.setMotor(100, dcMotor.FORWARD)
        DCRight.setMotor(20, dcMotor.FORWARD)
    elif data == 'BR':
        DCLeft.setMotor(20, dcMotor.BACKWARD)
        DCRight.setMotor(100, dcMotor.BACKWARD)
    elif data == 'BL':
        DCLeft.setMotor(100, dcMotor.BACKWARD)
        DCRight.setMotor(20, dcMotor.BACKWARD)
"""
    elif data == 'FL':
        DCLeft.setMotor(100, dcMotor.STOP)
        DCRight.setMotor(100, dcMotor.FORWARD)
    elif data == 'FR':
        DCLeft.setMotor(100, dcMotor.FORWARD)
        DCRight.setMotor(100, dcMotor.STOP)
    elif data == 'BL':
        DCLeft.setMotor(100, dcMotor.STOP)
        DCRight.setMotor(100, dcMotor.BACKWARD)
    elif data == 'BR':
        DCLeft.setMotor(100, dcMotor.BACKWARD)
        DCRight.setMotor(100, dcMotor.STOP)
"""

#################### UDP Recevied Process ####################
"""
    prev_udp_recv_order는 현재가 아닌 이전에 수행한 DC모터 제어 (예로 'F', 'B'와 같은 데이터가 저장됨)
    prev_udp_recv_time는 이전에 수행했던 명령이 언제 시작했는 지 기억하기 위한 변수들
    
    Waypoint_Append(currentTime): 
        UDP 데이터에서 새로운 방향의 데이터가 들어오거나 UDP가 신호가 일정 이상 들어오지 않는 경우
        이전에 기억하고 있는 prev_udp_recv_order(현재가 아닌 이전 명령)과 현재 시간에서 이전에 수행을 시작한 시간을 뺀 시간(즉, 해당 명령을 동작한 시간[예로 2초동안 앞으로])
        두 변수를 tmpArray에 임시로 배열을 지정한다. [앞으로, 2초]
        Waypoints에 맨 끝에 tmpArray를 넣어준다 [[정지, 10초], [앞으로, 2초]]
        
    UDP_Recevied_Process(server, ip, port, data)
        HEY라는 UDP 데이터를 받으면 OK라 답변해 통신에 이상 없음을 확인해준다
        복귀 모드를 종료하고 이전의 명령, 이전 명령이 시작 시간, 이전 이동을 기억한 Waypoints를 초기화 해준다.
                
        [! 삭제될 수 있는 사안] 만약 어떠한 문제로 잠시 동안 UDP 제어가 받아지지 않다가 받아지면
        복귀 모드를 종료하고 이전의 명령, 이전 명령이 시작 시간, 이전 이동을 기억한 Waypoints를 초기화 해준다.
        
        현재 시간을 기록하고 
        DC모터 제어 신호(앞, 뒤, 왼, 오 등...)가 확인되면 DC모터를 제어하고
        만약 위 제어 신호가 아니라면 현재 시간 기록을 삭제한다.
        
        현재 시간 기록이 존재한다면 현재 명령이 이전 명령과 다른지 확인하여 다르다면 Waypoint를 기록한다
"""
prev_udp_recv_order = 0
prev_udp_recv_time = 0.0

def Waypoint_Append(currentTime):
    global Waypoints, prev_udp_recv_order, prev_udp_recv_time
    tmpGetTick = currentTime - prev_udp_recv_time   # 현재 명령 시간에서 이전 명령 시간을 빼서 이전에 이동한 시간 구하기
    tmpArray = [prev_udp_recv_order, tmpGetTick]
    Waypoints.append(tmpArray)
    prev_udp_recv_order = 0

def UDP_Recevied_Process(server, ip, port, data):
    global UDP_LastRecvTime, ReturningMode, Waypoints, prev_udp_recv_order, prev_udp_recv_time
    if data == 'HEY':   # 초기 세팅
        server.send(ip, port, 'OK'.encode())
        ReturningMode = False
        prev_udp_recv_order = 0
        prev_udp_recv_time  = 0.0
        if len(Waypoints) > 0:
            del Waypoints[:]
    else:
        if ReturningMode == True:
            ReturningMode = False
            prev_udp_recv_order = 0
            prev_udp_recv_time  = 0.0
            if len(Waypoints) > 0:
                del Waypoints[:]

        GetTick = time.time()
        if (data == 'S' or data == 'F' or data == 'B' or data == 'L' or data == 'R' or
                data == 'FL' or data == 'FR' or data == 'BL' or data == 'BR'):
            DCMotor_Set(data)
            
            UDP_LastRecvTime = GetTick
            if prev_udp_recv_order != data:     # 이전 명령과 현재 명령어 다를 경우 기록
                if prev_udp_recv_order != 0:    # 맨 끝난 명령만 기억하므로 현재 명령은 무시
                    Waypoint_Append(GetTick)
                prev_udp_recv_order = data
                prev_udp_recv_time = GetTick

#################### TCP Recevied/Close Process ####################
"""
    TCP_Recevied_Process(sock, data)
        서보모터 제어는 "V|123", "H|250" 과 같이 수직이면 "V", 수평이면 "H"이며 그 뒤의 각도와 같이 데이터가 받아진다
        받은 데이터를 V와 123, H와 250으로 따로 따로 나누고
        나누어진 값으로 수평, 수직 서보모터를 제어한다
"""

TCP_Buffer = ''
def TCP_Recevied_Process(sock, data):
    global TCP_Buffer
    TCP_Buffer += data
    if '\n' in data: # 데이터의 끝이라면
        tmp_data = data.split('|')  # | 기준으로 명령어를 구분

        for lines in tmp_data:
            if lines == 'REQUEST':
                clearSensorData()   # 이전에 전송되었던 데이터 값 초기화
            elif 'V:' in lines: # 분활된 문자열 중 V:가 포함되었다면
                servo_order = int(lines[2:])    # V|100이면 100만 가져옴
                if (30 <= servo_order) and (servo_order <= 180): # 제어 범위를 넘으면 제어하지 않도록
                    servoMotor1.Set(servo_order)
            elif 'H:' in lines: # 분활된 문자열 중 H:가 포함되었다면
                servo_order = int(lines[2:])    # H|300이면 300만 가져옴
                if (30 <= servo_order) and (servo_order <= 270): # 제어 범위를 넘으면 제어하지 않도록
                    servoMotor2.Set(servo_order)

def TCP_Close_Process():
    #global dcMotor.FORWARD, dcMotor.BACKWARD, dcMotor.STOP
    print('Close TCP')
    
#################### UDP Handler     #########################
# def on_started(self, server):
# def on_stopped(self, server):
# def on_received(self, server, addr, data):
# def on_sent(self, server, status, data):
class EchoUdpHanlder(IUdpCallback):
    def __init__(self):
        pass

    # called when UDP server is started
    def on_started(self, server):
        print('UDP Server is started')

    # called when UDP server is stopped
    def on_stopped(self, server):
        print('UDP Server is stopped')

    # called when new packet arrived
    def on_received(self, server, addr, data):
        #print('UDP Received : ' + str(data) + '(' + data.decode() + ')' + ' From ' + addr[0] + ':' + str(addr[1]))
        UDP_Recevied_Process(server, addr[0], addr[1], data.decode())
        #print('Received : ' + str(data.decode())) # Python3

    # called when packet is sent
    def on_sent(self, server, status, data):
        print('UDP Send (' + str(status) + '): ' + str(data))

#################### TCP Handler     #########################
# def on_newconnection(self, sock, err):
# def on_disconnect(self, sock):
# def on_received(self, sock, data):
# def on_sent(self, sock, status, data):
class EchoSocketHandler(ITcpSocketCallback):
    def __init__(self):
        pass

    # called when new client connected to the server
    def on_newconnection(self, sock, err):
        print('New connection made')

    # called when the client disconnected
    def on_disconnect(self, sock):
        print('Client disconnected')
        TCP_Close_Process()

    # called when new packet arrived from the client
    def on_received(self, sock, data):
        #host, port = sock.socket.getpeername() # erasersetMotor
        #print('TCP Received : ' + str(data) + ' From ' + host + ':' + str(port))
        print('TCP Received : ' + str(data.decode())) # Python3
        #sock.send(data)        
        TCP_Recevied_Process(sock, data.decode())

    # called when packet is sent
    def on_sent(self, sock, status, data):
        print('TCP Send (' + str(status) + '): ' + str(data))
        
# def on_started(self, server):
# def on_accepted(self, server, sock)
# def on_stopped(self, server):
class EchoServerHandler(ITcpServerCallback):
    def __init__(self):
        pass

    # called when server finished the initialization and started listening
    def on_started(self, server):
        print('TCP server is started')

    # called when new client accepted
    def on_accepted(self, server, sock):
        print('New socket is accepted')

    # called when the server is stopped listening and disconnect all the clients
    def on_stopped(self, server):
        print('TCP server is stopped')

# def on_accept(self, server, addr):
# def get_socket_callback(self):
class EchoAcceptorHandler(IAcceptor):
    def __init__(self):
        self.handler=EchoSocketHandler()

    # called when new client connected
    # must return boolean : True to accept the connection otherwise reject
    def on_accept(self, server, addr):
        return True # accept always

    # called when the server accepted the client and ask for the handler
    # Must return ITcpSocketCallback object
    def get_socket_callback(self):
        return self.handler

###################### Threading  ############################

"""
UDP_LastRecvTime = 0.0  # UDP 마지막으로 받은 시간
ReturningMode = False   # 드론 복귀 모드 
Waypoints = []          # 복귀를 위한 Waypoint 변수 선언 [ [이동방향, 시간], ... ]

    LostCheckThread()
        저장된 Waypoints가 없으면 루프를 다시 돈다
        
        복귀모드가 꺼져있고 현재시간과 마지막으로 UDP 수신받은 시간을 빼서 250ms이상이면 모터를 정지하고 마지막으로 수행한 모터제어 명령을 Waypoints에 기록한다

        복귀모드가 꺼져있고 현재시간과 마지막으로 UDP 수신받은 시간을 빼서 30초 이상이면 복귀모드를 수행한다
        
        복귀모드가 수행 중이면 반복문을 수행한다
            ReturningMode가 여전히 True인지 Waypoints에 저장된 기록들이 남아있는 지 확인 후 루프를 수행한다
            Waypoints.pop()을 수행하여 맨 마지막으로 저장된 값을 가져오고 Waypoints의 맨 마지막에 저장된 값을 지워준다
            runDCMotor에 Waypoints의 맨 마지막 값이 저장되어 있는 데, 수행한 명령을 반대로 바꿔준다
            Delay를 사용하여 해당 명령이 수행한 시간동안 루프를 대기한다 (만약, 새로운 UDP 명령이 들어오면 모터 제어를 해당 루프 내에서 계속 바꾸는 것이 아니라 제어에 영향 없어 보임)
"""
def LostCheckThread():
    global ReturningMode, UDP_LastRecvTime, Waypoints
    while True:
        time.sleep(0.001) # 1ms

        if len(Waypoints) == 0:
            continue
        
        if ReturningMode == False and (time.time() - UDP_LastRecvTime > 5.0): # 복귀 모드가 꺼져있고 5초 동안 UDP 데이터가 없으면
            ReturningMode = True
            
        elif ReturningMode == False and (time.time() - UDP_LastRecvTime > 0.25): # 복귀 모드가 꺼져있고 250ms 동안 UDP 데이터가 없으면
            if prev_udp_recv_order != 0:    # 맨 끝난 명령만 기억하므로 현재 명령은 무시
                DCMotor_Set('S')    # 모터 정지
                Waypoint_Append(time.time())

        elif ReturningMode == True:
            while ReturningMode == True and len(Waypoints) > 0: # 수정필요 
                runDCMotor = Waypoints.pop()
                
                if runDCMotor[0] == 'F':
                    runDCMotor[0] = 'B'
                elif runDCMotor[0] == 'B':
                    runDCMotor[0] = 'F'
                elif runDCMotor[0] == 'L':
                    runDCMotor[0] = 'R'
                elif runDCMotor[0] == 'R':
                    runDCMotor[0] = 'L'
                elif runDCMotor[0] == 'FL':
                    runDCMotor[0] = 'BL'
                elif runDCMotor[0] == 'FR':
                    runDCMotor[0] = 'BR'
                elif runDCMotor[0] == 'BL': 
                    runDCMotor[0] = 'FL'
                elif runDCMotor[0] == 'BR':
                    runDCMotor[0] = 'FR'
                elif runDCMotor[0] == 'S':
                    DCMotor_Set('S')
                    time.sleep(1);
                    continue
                
                DCMotor_Set(runDCMotor[0])
                time.sleep(runDCMotor[1])

            if len(Waypoints) == 0:
                ReturningMode = False
                DCMotor_Set('S')
"""
    MainThread()
        초음파센서 값과 온습도계 값, 전력상태를 확인하고 send_data라는 하나의 변수로 묶어서 보내준다
        
        초음파센석 값은 50ms마다 확인하여 컴퓨터로 보내준 거리 값과 1이상 차이나야 전송된다
        온습도계 값은 1s마다 확인하여 컴퓨터로 보내준 거리 값과 1이상 차이나야 전송된다
        파워상태 값은 500ms마다 확인하여 전력에 이상이 생기면 값을 보내주며 정상일 때는 별도로 보내주지 않는다
        send_data에 데이터가 존재하면 TCP 소켓에 있는 유저 처음 한 사람에게만 보내준다
"""

# 초기 파워 상태 구하기
power_lastStatus = 300
# Ultrasonic 기록된 거리 값
# Ultrasonic TCP로 전송된 거리 값
us_currentDis = [ 0, 0 ]
us_prevDis = [ 0, 0 ]
# 온습도계 보내진 값
th_prevData = [ 0, 0 ]
# TCP sender Buffer

# clearSesorData(): 이전에 보낸 데이터 값을 초기화해 다시 센서 값을 보내줌
def clearSensorData():
    global power_lastStatus, us_prevDis, th_prevData
    power_lastStatus = 300
    us_prevDis = [ 0, 0 ]
    th_prevData = [ 0, 0 ]

def MainThread():
    global power_lastStatus, us_currentDis, us_prevDis, th_prevData
    us_lastPoll = 0.0
    dht_lastPoll = 0.0
    power_lastPoll = 0.0
    while True:
        currentTime = time.time()
        send_data = ''

        if (currentTime - us_lastPoll) >= 0.05: # 50ms
            us_lastPoll = currentTime
            us_currentDis[0] = ultrasonic1.getDistance()
            us_currentDis[1] = ultrasonic2.getDistance()
            #print('us1:' + str(us_currentDis[0]) + '  us2:' + str(us_currentDis[1]))
            us_send_lastPoll = currentTime
            for i in range(len(us_currentDis)):
                if us_currentDis[i] != 0 and abs(us_prevDis[i] - us_currentDis[i]) >= 1: # 현재 값과 이전에 보내진 값이 1이상 차이날 경우
                    us_prevDis[i] = us_currentDis[i]
                    send_data += 'US' + str(i) + ':' + str(int(us_currentDis[i])) + '|'

        if (currentTime - dht_lastPoll) >= 1 : # 1sec
            dht_lastPoll = currentTime
            result = temperature.read()
            tmp_reulst = [ int(result.temperature), int(result.humidity) ]
            #print('Temmp:' + str(result.temperature) + '  Humid:' + str(result.humidity))
            if result.is_valid():
                for i in range(len(th_prevData)):
                    if tmp_reulst[i] > 0 and abs(th_prevData[i] - tmp_reulst[i]) >= 1: # 현재 값과 이전에 보내진 값이 1이상 차이날 경우
                        th_prevData[i] = tmp_reulst[i]
                        send_data += 'TH' + str(i) + ':' + str(tmp_reulst[i]) + '|'

        if (currentTime - power_lastPoll) >= 0.5: # 500ms
            power_lastPoll = currentTime
            stream = os.popen('cat /sys/devices/platform/leds/leds/led1/brightness')
            output = int(stream.read())
            if output != power_lastStatus:
                power_lastStatus = output
                send_data += 'POWER:' + str(power_lastStatus) + '|'

        if len(send_data) > 0: 
            for socket in tcp_server.get_socket_list():
                #data_length = len(send_data)
                #send_data = '$' + str(data_length) + '|' + send_data
                send_data += '\n'
                socket.send(send_data.encode())
                break
                
        #time.sleep(0.001) # 1ms 

#######################  Main  ###############################
"""
    UDP 서버를 50001포트에서 활성화한다.
    TCP 서버를 50002포트에서 활성화한다.
    MainThread, LostCheckThread 함수를 multi threading으로 사용한다.
"""

port = 50001
handler = EchoUdpHanlder()
bind_addr = ''
udp_server = AsyncUDP(port, handler, bind_addr)

port = 50002
acceptor = EchoAcceptorHandler()
server_handler = EchoServerHandler()
bind_addr = ''
no_delay = True # If True, Nagle's algorithm is not used, otherwise use Nagle's Algorithm
tcp_server = AsyncTcpServer(port, server_handler, acceptor, bind_addr, no_delay)

t1 = threading.Thread(target=MainThread, args=())
t1.daemon = True
t1.start()

t2 = threading.Thread(target=LostCheckThread, args=())
t2.daemon = True
t2.start()

"""
while True:
    try:
        time.sleep(5)
    except KeyboardInterrupt:
        print('Program Interrupt')
        GPIO.cleanup()
        sys.exit(0)
GPIO.cleanup()
"""

def signal_handler(sig, frame):
    GPIO.cleanup()
    sys.exit(0)

if __name__ == '__main__':
    signal.signal(signal.SIGINT, signal_handler)
    signal.pause()