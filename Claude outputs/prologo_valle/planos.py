# -*- coding: utf-8 -*-
import sys; sys.path.insert(0,'.')
from shotmath import Actor, describe

def escena(lado, grados):
    print("\n══ eje de acción fijado a %d° %s ══" % (grados, lado))
    L=(__import__("math").sin(__import__("math").radians(grados)),0,__import__("math").cos(__import__("math").radians(grados)))
    A=lambda p,m: Actor("x",p,m)

    # P1 horno
    mago=A((5995.5,100,6002.3),(5995.5,100,6003.8)); horno=Actor("horno",(5995.5,100,6003.8),eye=0.8,radio=0.7)
    describe("1 horno · dos (mago+horno)","TwoShot",mago,horno,lado_fijado=L)
    describe("1 horno · medio mago","Medium",mago,horno,lado_fijado=L)
    describe("1 horno · OTS al horno","OTS",horno,mago,lado_fijado=L)

    # P2 carreta
    mago=A((6003.2,100,5999.6),(6004.5,100,5998.2)); car=Actor("carreta",(6004.5,100,5998.2),eye=0.9,radio=1.2)
    describe("2 carreta · dos","TwoShot",mago,car,lado_fijado=L)
    describe("2 carreta · primer plano","CloseUp",mago,car,lado_fijado=L)

    # P3 globo
    mago=A((5990.5,100,6001.6),(5987.2,100,6003.4)); globo=Actor("globo",(5987.2,105.8,6003.4),eye=0,radio=0.8)
    describe("3 globo · medio mago","Medium",mago,globo,lado_fijado=L)
    describe("3 globo · general campanario","Wide",globo,mago,lado_fijado=L)

    # P4 carta
    mago=A((6010.6,100,6001.9),(6012.3,100,6003.4)); liora=A((6012.3,100,6003.4),(6010.6,100,6001.9))
    describe("4 carta · dos mago+Liora","TwoShot",mago,liora,lado_fijado=L)
    describe("4 carta · primer plano mago","CloseUp",mago,liora,lado_fijado=L)

    # P5 viga
    mago=A((6005,100,5999.2),(6006,100.5,6000.5)); viga=Actor("viga",(6006,100.5,6000.5),eye=1.2,radio=1.5)
    describe("5 viga · general","Wide",mago,viga,lado_fijado=L)
    describe("5 viga · medio mago","Medium",mago,viga,lado_fijado=L)

    # P6 despedida
    mago=A((6016.6,100,6000.8),(6018.2,100,6000.4)); liora=A((6018.2,100,6000.4),(6016.6,100,6000.8))
    describe("6 despedida · dos","TwoShot",mago,liora,lado_fijado=L)
    describe("6 despedida · reacción Liora","Reaction",liora,mago,lado_fijado=L)

    # P7 duelo
    mago=A((6002,100,6001.5),(6002,100,6008)); oscuro=A((6002,100,6008),(6002,100,6001.5))
    describe("7 duelo · general","Wide",oscuro,mago,lado_fijado=L)
    describe("7 duelo · primer plano mago","CloseUp",mago,oscuro,lado_fijado=L)
    describe("7 duelo · primer plano oscuro","CloseUp",oscuro,mago,lado_fijado=L)

for g,l in ((0,"(+Z norte)"),(90,"(+X este)"),(180,"(-Z sur)"),(270,"(-X oeste)")):
    escena(l,g)
